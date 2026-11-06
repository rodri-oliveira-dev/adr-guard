import type { RelationshipGraphReport, RelationshipResolution, RelationshipType } from '../contracts/relationshipGraph';

export interface GraphPoint { readonly x: number; readonly y: number }
export interface GraphLayoutNode extends GraphPoint {
  readonly id: string; readonly path: string; readonly title: string | null; readonly status: string | null;
  readonly format: 'canonical' | 'madr-4'; readonly cycle: boolean; readonly broken: boolean; readonly ambiguous: boolean;
}
export interface GraphLayoutEdge {
  readonly source: GraphPoint; readonly target: GraphPoint | null; readonly sourcePath: string; readonly targetPath: string | null;
  readonly type: RelationshipType; readonly resolution: RelationshipResolution; readonly declaredTarget: string; readonly cycle: boolean;
}
export interface RelationshipGraphViewModel {
  readonly width: number; readonly height: number; readonly nodes: readonly GraphLayoutNode[]; readonly edges: readonly GraphLayoutEdge[];
  readonly totalMatchingNodes: number; readonly truncated: boolean;
}
export interface RelationshipGraphFilters { readonly status: string | null; readonly type: RelationshipType | null; readonly focusPath: string | null }

export const maximumVisibleRelationshipGraphNodes = 100;
const nodeWidth = 220;
const nodeHeight = 76;
const columnGap = 100;
const rowGap = 42;
const margin = 40;

export function relationshipGraphStatuses(report: RelationshipGraphReport): readonly string[] {
  return [...new Set(report.nodes.flatMap((node) => node.status === null ? [] : [node.status]))]
    .sort((left, right) => left.localeCompare(right, 'en', { sensitivity: 'base' }));
}

export function layoutRelationshipGraph(
  report: RelationshipGraphReport,
  filters: RelationshipGraphFilters = { status: null, type: null, focusPath: null },
): RelationshipGraphViewModel {
  const matching = filteredNodes(report, filters);
  const ordered = matching.slice(0, maximumVisibleRelationshipGraphNodes);
  const visiblePaths = new Set(ordered.map((node) => node.path));
  const relationships = report.relationships.filter((relationship) =>
    (filters.type === null || relationship.type === filters.type) && visiblePaths.has(relationship.sourcePath));
  const byKey = new Map(ordered.map((node) => [key(node.id, node.path), node]));
  const outgoing = new Map(ordered.map((node) => [key(node.id, node.path), [] as string[]]));
  const indegree = new Map(ordered.map((node) => [key(node.id, node.path), 0]));
  for (const relationship of relationships) for (const target of relationship.targets) {
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
  for (const node of ordered) {
    const rank = ranks.get(key(node.id, node.path)) ?? 0;
    columns.set(rank, [...(columns.get(rank) ?? []), node]);
  }
  const positions = new Map<string, GraphPoint>();
  let maximumRows = 1;
  for (const [rank, nodes] of [...columns].sort(([left], [right]) => left - right)) {
    nodes.sort(compareNode); maximumRows = Math.max(maximumRows, nodes.length);
    nodes.forEach((node, row) => positions.set(key(node.id, node.path), { x: margin + rank * (nodeWidth + columnGap), y: margin + row * (nodeHeight + rowGap) }));
  }
  const cyclePaths = new Set(report.diagnostics.filter((item) => item.code === 'ADR010').map((item) => item.path));
  const brokenPaths = new Set(report.relationships.filter((item) => item.resolution === 'unresolved').map((item) => item.sourcePath));
  const ambiguousPaths = new Set(report.relationships.filter((item) => item.resolution === 'ambiguous').map((item) => item.sourcePath));
  const layoutNodes = ordered.map((node) => ({
    ...(positions.get(key(node.id, node.path)) ?? { x: margin, y: margin }), id: node.id, path: node.path, title: node.title,
    status: node.status, format: node.format, cycle: cyclePaths.has(node.path), broken: brokenPaths.has(node.path), ambiguous: ambiguousPaths.has(node.path),
  }));
  const edges: GraphLayoutEdge[] = [];
  for (const relationship of [...relationships].sort(compareRelationship)) {
    const sourcePosition = positions.get(key(relationship.sourceId, relationship.sourcePath));
    if (sourcePosition === undefined) continue;
    const source = { x: sourcePosition.x + nodeWidth, y: sourcePosition.y + nodeHeight / 2 };
    if (relationship.targets.length === 0) edges.push({ source, target: null, sourcePath: relationship.sourcePath, targetPath: null, type: relationship.type, resolution: relationship.resolution, declaredTarget: relationship.declaredTarget, cycle: false });
    for (const target of [...relationship.targets].sort(compareTarget)) {
      const position = positions.get(key(target.id, target.path));
      if (position !== undefined) edges.push({
        source, target: { x: position.x, y: position.y + nodeHeight / 2 }, sourcePath: relationship.sourcePath, targetPath: target.path,
        type: relationship.type, resolution: relationship.resolution, declaredTarget: relationship.declaredTarget,
        cycle: cyclePaths.has(relationship.sourcePath) && cyclePaths.has(target.path) && relationship.type !== 'depends-on',
      });
    }
  }
  const maxRank = Math.max(0, ...ranks.values());
  return {
    width: margin * 2 + (maxRank + 1) * nodeWidth + maxRank * columnGap,
    height: margin * 2 + maximumRows * nodeHeight + (maximumRows - 1) * rowGap,
    nodes: layoutNodes, edges, totalMatchingNodes: matching.length, truncated: matching.length > maximumVisibleRelationshipGraphNodes,
  };
}

function filteredNodes(report: RelationshipGraphReport, filters: RelationshipGraphFilters): RelationshipGraphReport['nodes'][number][] {
  const focusedPaths = directPaths(report, filters);
  const typePaths = relationshipTypePaths(report, filters.type);
  return [...report.nodes]
    .filter((node) => (filters.status === null || node.status === filters.status)
      && (typePaths === undefined || typePaths.has(node.path))
      && (focusedPaths === undefined || focusedPaths.has(node.path) || node.path === filters.focusPath))
    .sort((left, right) => left.path === filters.focusPath ? -1 : right.path === filters.focusPath ? 1 : compareNode(left, right));
}

function relationshipTypePaths(report: RelationshipGraphReport, type: RelationshipType | null): Set<string> | undefined {
  if (type === null) return undefined;
  const result = new Set<string>();
  for (const relationship of report.relationships) {
    if (relationship.type !== type) continue;
    result.add(relationship.sourcePath);
    for (const target of relationship.targets) result.add(target.path);
  }
  return result;
}

function directPaths(report: RelationshipGraphReport, filters: RelationshipGraphFilters): Set<string> | undefined {
  if (filters.focusPath === null || !report.nodes.some((node) => node.path === filters.focusPath)) return undefined;
  const result = new Set([filters.focusPath]);
  for (const relationship of report.relationships) {
    if (filters.type !== null && relationship.type !== filters.type) continue;
    if (relationship.sourcePath === filters.focusPath) for (const target of relationship.targets) result.add(target.path);
    if (relationship.targets.some((target) => target.path === filters.focusPath)) result.add(relationship.sourcePath);
  }
  return result;
}

function compareNode(left: RelationshipGraphReport['nodes'][number], right: RelationshipGraphReport['nodes'][number]): number { return left.id.localeCompare(right.id, 'en', { sensitivity: 'case' }) || left.path.localeCompare(right.path, 'en', { sensitivity: 'case' }); }
function compareRelationship(left: RelationshipGraphReport['relationships'][number], right: RelationshipGraphReport['relationships'][number]): number { return left.sourceId.localeCompare(right.sourceId, 'en', { sensitivity: 'case' }) || left.sourcePath.localeCompare(right.sourcePath, 'en', { sensitivity: 'case' }) || left.type.localeCompare(right.type, 'en', { sensitivity: 'case' }) || left.declaredTarget.localeCompare(right.declaredTarget, 'en', { sensitivity: 'case' }); }
function compareTarget(left: { readonly id: string; readonly path: string }, right: { readonly id: string; readonly path: string }): number { return left.id.localeCompare(right.id, 'en', { sensitivity: 'case' }) || left.path.localeCompare(right.path, 'en', { sensitivity: 'case' }); }
function key(id: string, path: string): string { return `${id}\0${path}`; }

export const relationshipGraphNodeSize = { width: nodeWidth, height: nodeHeight } as const;
