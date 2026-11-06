import type { RelationshipGraphNode, RelationshipGraphRelationship, RelationshipGraphReport, RelationshipResolution, RelationshipType } from '../contracts/relationshipGraph';

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
export interface RelationshipGraphFilters {
  readonly query: string;
  readonly status: string | null;
  readonly type: RelationshipType | null;
  readonly focusPath: string | null;
}

export const maximumVisibleRelationshipGraphNodes = 100;
export const emptyRelationshipGraphFilters: RelationshipGraphFilters = { query: '', status: null, type: null, focusPath: null };
const nodeWidth = 220;
const nodeHeight = 76;
const columnGap = 100;
const rowGap = 42;
const margin = 40;

export function relationshipGraphStatuses(report: RelationshipGraphReport): readonly string[] {
  return [...new Set(report.nodes.flatMap((node) => node.status === null || node.status.length === 0 ? [] : [node.status]))]
    .sort(compareText);
}

export function layoutRelationshipGraph(
  report: RelationshipGraphReport,
  filters: RelationshipGraphFilters = emptyRelationshipGraphFilters,
): RelationshipGraphViewModel {
  const matching = filteredNodes(report, filters);
  const ordered = matching.slice(0, maximumVisibleRelationshipGraphNodes);
  const visiblePaths = new Set(ordered.map((node) => node.path));
  const relationships = visibleRelationships(report, filters.type, visiblePaths);
  const { outgoing, indegree } = createTopology(ordered, relationships);
  const ranks = calculateRanks(ordered, outgoing, indegree);
  const { positions, maximumRows } = positionNodes(ordered, ranks);
  const cyclePaths = new Set(report.diagnostics.filter((item) => item.code === 'ADR010').map((item) => item.path));
  const cycleEdges = relationshipCycleEdges(report);
  const layoutNodes = createLayoutNodes(ordered, positions, report, cyclePaths);
  const edges = createLayoutEdges(relationships, positions, cyclePaths, cycleEdges);
  return {
    width: calculateWidth(layoutNodes, edges),
    height: margin * 2 + maximumRows * nodeHeight + (maximumRows - 1) * rowGap,
    nodes: layoutNodes,
    edges,
    totalMatchingNodes: matching.length,
    truncated: matching.length > maximumVisibleRelationshipGraphNodes,
  };
}

function visibleRelationships(report: RelationshipGraphReport, type: RelationshipType | null, visiblePaths: ReadonlySet<string>): RelationshipGraphRelationship[] {
  return report.relationships.filter((relationship) =>
    (type === null || relationship.type === type) && visiblePaths.has(relationship.sourcePath));
}

function createTopology(nodes: readonly RelationshipGraphNode[], relationships: readonly RelationshipGraphRelationship[]): {
  readonly outgoing: Map<string, string[]>;
  readonly indegree: Map<string, number>;
} {
  const visibleKeys = new Set(nodes.map((node) => key(node.id, node.path)));
  const outgoing = new Map(nodes.map((node) => [key(node.id, node.path), [] as string[]]));
  const indegree = new Map(nodes.map((node) => [key(node.id, node.path), 0]));
  for (const relationship of relationships) {
    const sourceKey = key(relationship.sourceId, relationship.sourcePath);
    for (const target of relationship.targets) {
      const targetKey = key(target.id, target.path);
      if (sourceKey === targetKey || !visibleKeys.has(sourceKey) || !visibleKeys.has(targetKey)) continue;
      outgoing.get(sourceKey)?.push(targetKey);
      indegree.set(targetKey, (indegree.get(targetKey) ?? 0) + 1);
    }
  }
  for (const targets of outgoing.values()) targets.sort(compareText);
  return { outgoing, indegree };
}

function calculateRanks(
  nodes: readonly RelationshipGraphNode[],
  outgoing: ReadonlyMap<string, readonly string[]>,
  indegree: Map<string, number>,
): Map<string, number> {
  const ranks = new Map(nodes.map((node) => [key(node.id, node.path), 0]));
  const queue = nodes.map((node) => key(node.id, node.path)).filter((nodeKey) => indegree.get(nodeKey) === 0).sort(compareText);
  while (queue.length > 0) {
    const source = queue.shift();
    if (source === undefined) break;
    for (const target of outgoing.get(source) ?? []) {
      ranks.set(target, Math.max(ranks.get(target) ?? 0, (ranks.get(source) ?? 0) + 1));
      const remaining = (indegree.get(target) ?? 1) - 1;
      indegree.set(target, remaining);
      if (remaining === 0) {
        queue.push(target);
        queue.sort(compareText);
      }
    }
  }
  return ranks;
}

function positionNodes(nodes: RelationshipGraphNode[], ranks: ReadonlyMap<string, number>): {
  readonly positions: Map<string, GraphPoint>;
  readonly maximumRows: number;
} {
  const columns = new Map<number, RelationshipGraphNode[]>();
  for (const node of nodes) {
    const rank = ranks.get(key(node.id, node.path)) ?? 0;
    columns.set(rank, [...(columns.get(rank) ?? []), node]);
  }
  const positions = new Map<string, GraphPoint>();
  let maximumRows = 1;
  for (const [rank, columnNodes] of [...columns].sort(([left], [right]) => left - right)) {
    columnNodes.sort(compareNode);
    maximumRows = Math.max(maximumRows, columnNodes.length);
    columnNodes.forEach((node, row) => positions.set(
      key(node.id, node.path),
      { x: margin + rank * (nodeWidth + columnGap), y: margin + row * (nodeHeight + rowGap) },
    ));
  }
  return { positions, maximumRows };
}

function createLayoutNodes(
  nodes: readonly RelationshipGraphNode[],
  positions: ReadonlyMap<string, GraphPoint>,
  report: RelationshipGraphReport,
  cyclePaths: ReadonlySet<string>,
): GraphLayoutNode[] {
  const brokenPaths = new Set(report.relationships.filter((item) => item.resolution === 'unresolved').map((item) => item.sourcePath));
  const ambiguousPaths = new Set(report.relationships.filter((item) => item.resolution === 'ambiguous').map((item) => item.sourcePath));
  return nodes.map((node) => ({
    ...(positions.get(key(node.id, node.path)) ?? { x: margin, y: margin }),
    id: node.id,
    path: node.path,
    title: node.title,
    status: node.status,
    format: node.format,
    cycle: cyclePaths.has(node.path),
    broken: brokenPaths.has(node.path),
    ambiguous: ambiguousPaths.has(node.path),
  }));
}

function createLayoutEdges(
  relationships: readonly RelationshipGraphRelationship[],
  positions: ReadonlyMap<string, GraphPoint>,
  cyclePaths: ReadonlySet<string>,
  cycleEdges: ReadonlySet<string>,
): GraphLayoutEdge[] {
  const edges: GraphLayoutEdge[] = [];
  for (const relationship of [...relationships].sort(compareRelationship)) {
    const sourcePosition = positions.get(key(relationship.sourceId, relationship.sourcePath));
    if (sourcePosition === undefined) continue;
    const source = { x: sourcePosition.x + nodeWidth, y: sourcePosition.y + nodeHeight / 2 };
    if (relationship.targets.length === 0) edges.push(edge(relationship, source, null, null, false));
    for (const target of [...relationship.targets].sort(compareTarget)) {
      const position = positions.get(key(target.id, target.path));
      if (position === undefined) continue;
      const participatesInCycle = cyclePaths.has(relationship.sourcePath)
        && cyclePaths.has(target.path)
        && cycleEdges.has(cycleEdgeKey(relationship.sourcePath, target.path, relationship.type));
      edges.push(edge(relationship, source, { x: position.x, y: position.y + nodeHeight / 2 }, target.path, participatesInCycle));
    }
  }
  return edges;
}

function edge(
  relationship: RelationshipGraphRelationship,
  source: GraphPoint,
  target: GraphPoint | null,
  targetPath: string | null,
  cycle: boolean,
): GraphLayoutEdge {
  return {
    source,
    target,
    sourcePath: relationship.sourcePath,
    targetPath,
    type: relationship.type,
    resolution: relationship.resolution,
    declaredTarget: relationship.declaredTarget,
    cycle,
  };
}

function relationshipCycleEdges(report: RelationshipGraphReport): Set<string> {
  const adjacency = new Map(report.nodes.map((node) => [node.path, [] as string[]]));
  const reverse = new Map(report.nodes.map((node) => [node.path, [] as string[]]));
  for (const relationship of report.relationships) {
    if (relationship.type === 'depends-on' || relationship.resolution !== 'resolved') continue;
    for (const target of relationship.targets) {
      const [from, to] = relationship.type === 'supersedes'
        ? [target.path, relationship.sourcePath]
        : [relationship.sourcePath, target.path];
      adjacency.get(from)?.push(to);
      reverse.get(to)?.push(from);
    }
  }
  for (const targets of [...adjacency.values(), ...reverse.values()]) targets.sort(compareText);
  const components = assignComponents(finishingOrder(adjacency), reverse);
  const result = new Set<string>();
  for (const relationship of report.relationships) {
    if (relationship.type === 'depends-on' || relationship.resolution !== 'resolved') continue;
    for (const target of relationship.targets) {
      if (components.get(relationship.sourcePath) === components.get(target.path)) {
        result.add(cycleEdgeKey(relationship.sourcePath, target.path, relationship.type));
      }
    }
  }
  return result;
}

function finishingOrder(adjacency: ReadonlyMap<string, readonly string[]>): string[] {
  const visited = new Set<string>();
  const order: string[] = [];
  for (const start of [...adjacency.keys()].sort(compareText)) {
    const stack: { readonly path: string; readonly expanded: boolean }[] = [{ path: start, expanded: false }];
    while (stack.length > 0) {
      const current = stack.pop();
      if (current === undefined) break;
      if (current.expanded) {
        order.push(current.path);
        continue;
      }
      if (visited.has(current.path)) continue;
      visited.add(current.path);
      stack.push({ path: current.path, expanded: true });
      const targets = adjacency.get(current.path) ?? [];
      for (let index = targets.length - 1; index >= 0; index--) {
        const target = targets[index];
        if (target !== undefined && !visited.has(target)) stack.push({ path: target, expanded: false });
      }
    }
  }
  return order;
}

function assignComponents(order: readonly string[], reverse: ReadonlyMap<string, readonly string[]>): Map<string, number> {
  const components = new Map<string, number>();
  let component = 0;
  for (const start of [...order].reverse()) {
    if (components.has(start)) continue;
    const stack = [start];
    components.set(start, component);
    while (stack.length > 0) {
      const current = stack.pop();
      if (current === undefined) break;
      for (const target of reverse.get(current) ?? []) {
        if (components.has(target)) continue;
        components.set(target, component);
        stack.push(target);
      }
    }
    component++;
  }
  return components;
}

function calculateWidth(nodes: readonly GraphLayoutNode[], edges: readonly GraphLayoutEdge[]): number {
  const nodeExtent = Math.max(nodeWidth + margin, ...nodes.map((node) => node.x + nodeWidth));
  const edgeExtent = Math.max(0, ...edges.map((item) => {
    if (item.target === null) return item.source.x + 80;
    const curve = Math.max(32, Math.abs(item.target.x - item.source.x) / 2);
    return Math.max(item.source.x + curve, item.target.x + 10);
  }));
  return Math.max(nodeExtent, edgeExtent) + margin;
}

function filteredNodes(report: RelationshipGraphReport, filters: RelationshipGraphFilters): RelationshipGraphNode[] {
  const focusedPaths = directPaths(report, filters);
  const typePaths = relationshipTypePaths(report, filters.type);
  const query = filters.query.trim().toLocaleLowerCase('en-US');
  return [...report.nodes]
    .filter((node) => matchesQuery(node, query)
      && (filters.status === null || node.status === filters.status)
      && (typePaths === undefined || typePaths.has(node.path))
      && (focusedPaths === undefined || focusedPaths.has(node.path)))
    .sort((left, right) => compareFocusedNode(left, right, filters.focusPath));
}

function matchesQuery(node: RelationshipGraphNode, query: string): boolean {
  if (query.length === 0) return true;
  return [node.id, node.title ?? '', node.path].some((value) => value.toLocaleLowerCase('en-US').includes(query));
}

function compareFocusedNode(left: RelationshipGraphNode, right: RelationshipGraphNode, focusPath: string | null): number {
  if (left.path === focusPath) return -1;
  if (right.path === focusPath) return 1;
  return compareNode(left, right);
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
    if (relationship.sourcePath === filters.focusPath) {
      for (const target of relationship.targets) result.add(target.path);
    }
    if (relationship.targets.some((target) => target.path === filters.focusPath)) result.add(relationship.sourcePath);
  }
  return result;
}

function compareNode(left: RelationshipGraphNode, right: RelationshipGraphNode): number {
  return compareText(left.id, right.id) || compareText(left.path, right.path);
}
function compareRelationship(left: RelationshipGraphRelationship, right: RelationshipGraphRelationship): number {
  return compareText(left.sourceId, right.sourceId)
    || compareText(left.sourcePath, right.sourcePath)
    || compareText(left.type, right.type)
    || compareText(left.declaredTarget, right.declaredTarget);
}
function compareTarget(left: { readonly id: string; readonly path: string }, right: { readonly id: string; readonly path: string }): number {
  return compareText(left.id, right.id) || compareText(left.path, right.path);
}
function compareText(left: string, right: string): number {
  if (left < right) return -1;
  if (left > right) return 1;
  return 0;
}
function cycleEdgeKey(sourcePath: string, targetPath: string, type: RelationshipType): string {
  return `${sourcePath}\0${targetPath}\0${type}`;
}
function key(id: string, path: string): string {
  return `${id}\0${path}`;
}

export const relationshipGraphNodeSize = { width: nodeWidth, height: nodeHeight } as const;
