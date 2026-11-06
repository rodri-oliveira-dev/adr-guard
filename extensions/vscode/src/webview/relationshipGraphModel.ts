import type { RelationshipGraphReport, RelationshipResolution, RelationshipType } from '../contracts/relationshipGraph';

export interface GraphPoint { readonly x: number; readonly y: number }
export interface GraphLayoutNode extends GraphPoint { readonly id: string; readonly path: string; readonly title: string | null; readonly status: string | null }
export interface GraphLayoutEdge { readonly source: GraphPoint; readonly target: GraphPoint | null; readonly type: RelationshipType; readonly resolution: RelationshipResolution; readonly declaredTarget: string }
export interface RelationshipGraphViewModel { readonly width: number; readonly height: number; readonly nodes: readonly GraphLayoutNode[]; readonly edges: readonly GraphLayoutEdge[] }

const nodeWidth = 220;
const nodeHeight = 76;
const columnGap = 100;
const rowGap = 42;
const margin = 40;

export function layoutRelationshipGraph(report: RelationshipGraphReport): RelationshipGraphViewModel {
  const ordered = [...report.nodes].sort(compareNode);
  const byKey = new Map(ordered.map((node) => [key(node.id, node.path), node]));
  const outgoing = new Map(ordered.map((node) => [key(node.id, node.path), [] as string[]]));
  const indegree = new Map(ordered.map((node) => [key(node.id, node.path), 0]));
  for (const relationship of report.relationships) for (const target of relationship.targets) {
    const sourceKey = key(relationship.sourceId, relationship.sourcePath);
    const targetKey = key(target.id, target.path);
    if (sourceKey !== targetKey && byKey.has(sourceKey) && byKey.has(targetKey)) {
      outgoing.get(sourceKey)?.push(targetKey);
      indegree.set(targetKey, (indegree.get(targetKey) ?? 0) + 1);
    }
  }
  for (const targets of outgoing.values()) targets.sort();
  const ranks = new Map(ordered.map((node) => [key(node.id, node.path), 0]));
  const queue = ordered.map((node) => key(node.id, node.path)).filter((nodeKey) => indegree.get(nodeKey) === 0).sort();
  while (queue.length > 0) {
    const source = queue.shift();
    if (source === undefined) break;
    for (const target of outgoing.get(source) ?? []) {
      ranks.set(target, Math.max(ranks.get(target) ?? 0, (ranks.get(source) ?? 0) + 1));
      const remaining = (indegree.get(target) ?? 1) - 1;
      indegree.set(target, remaining);
      if (remaining === 0) { queue.push(target); queue.sort(); }
    }
  }
  const columns = new Map<number, typeof ordered>();
  for (const node of ordered) { const rank = ranks.get(key(node.id, node.path)) ?? 0; columns.set(rank, [...(columns.get(rank) ?? []), node]); }
  const positions = new Map<string, GraphPoint>();
  let maximumRows = 1;
  for (const [rank, nodes] of [...columns].sort(([left], [right]) => left - right)) {
    nodes.sort(compareNode); maximumRows = Math.max(maximumRows, nodes.length);
    nodes.forEach((node, row) => positions.set(key(node.id, node.path), { x: margin + rank * (nodeWidth + columnGap), y: margin + row * (nodeHeight + rowGap) }));
  }
  const layoutNodes = ordered.map((node) => ({ ...positions.get(key(node.id, node.path)) ?? { x: margin, y: margin }, id: node.id, path: node.path, title: node.title, status: node.status }));
  const edges: GraphLayoutEdge[] = [];
  const orderedRelationships = [...report.relationships].sort((left, right) =>
    left.sourceId.localeCompare(right.sourceId, 'en', { sensitivity: 'case' })
    || left.sourcePath.localeCompare(right.sourcePath, 'en', { sensitivity: 'case' })
    || left.type.localeCompare(right.type, 'en', { sensitivity: 'case' })
    || left.declaredTarget.localeCompare(right.declaredTarget, 'en', { sensitivity: 'case' }));
  for (const relationship of orderedRelationships) {
    const sourcePosition = positions.get(key(relationship.sourceId, relationship.sourcePath));
    if (sourcePosition === undefined) continue;
    const source = { x: sourcePosition.x + nodeWidth, y: sourcePosition.y + nodeHeight / 2 };
    if (relationship.targets.length === 0) edges.push({ source, target: null, type: relationship.type, resolution: relationship.resolution, declaredTarget: relationship.declaredTarget });
    for (const target of [...relationship.targets].sort((left, right) => left.id.localeCompare(right.id, 'en', { sensitivity: 'case' }) || left.path.localeCompare(right.path, 'en', { sensitivity: 'case' }))) {
      const position = positions.get(key(target.id, target.path));
      if (position !== undefined) edges.push({ source, target: { x: position.x, y: position.y + nodeHeight / 2 }, type: relationship.type, resolution: relationship.resolution, declaredTarget: relationship.declaredTarget });
    }
  }
  const maxRank = Math.max(0, ...ranks.values());
  return { width: margin * 2 + (maxRank + 1) * nodeWidth + maxRank * columnGap, height: margin * 2 + maximumRows * nodeHeight + (maximumRows - 1) * rowGap, nodes: layoutNodes, edges };
}

function compareNode(left: RelationshipGraphReport['nodes'][number], right: RelationshipGraphReport['nodes'][number]): number { return left.id.localeCompare(right.id, 'en', { sensitivity: 'case' }) || left.path.localeCompare(right.path, 'en', { sensitivity: 'case' }); }
function key(id: string, path: string): string { return `${id}\0${path}`; }

export const relationshipGraphNodeSize = { width: nodeWidth, height: nodeHeight } as const;
