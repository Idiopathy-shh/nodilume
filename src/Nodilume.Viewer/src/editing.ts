export function localDragPosition(local: number[], start: number[], end: number[]): number[] {
  if ([local, start, end].some(v => v.length !== 3 || !v.every(Number.isFinite)))
    throw new RangeError('Drag geometry must be finite 3D vectors.');
  const result = local.map((value, i) => value + end[i] - start[i]);
  if (!result.every(Number.isFinite)) throw new RangeError('Drag overflow.');
  return result;
}
export function acceptsEditResult(pendingId: string | null, mapId: string | null,
  result: {requestId: string; mapId: string}): boolean {
  return pendingId !== null && mapId !== null
    && result.requestId === pendingId && result.mapId === mapId;
}
