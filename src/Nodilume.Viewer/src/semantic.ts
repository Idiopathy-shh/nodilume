export type SemanticThresholds = {
  enterDistance: number;
  exitDistance: number;
  dwellMs: number;
};

export const DEFAULT_SEMANTIC_THRESHOLDS: SemanticThresholds = {
  enterDistance: 68,
  exitDistance: 260,
  dwellMs: 280
};

export type SemanticSample = {
  candidateEligible: boolean;
  candidateDistance: number;
  stableMs: number;
  canExit: boolean;
  contextDistance: number;
};

function finiteVector(value: readonly number[]): boolean {
  return value.length === 3 && value.every(Number.isFinite);
}

export function reframePose(
  position: readonly number[],
  target: readonly number[],
  fromOrigin: readonly number[],
  toOrigin: readonly number[]
): {position: number[]; target: number[]} {
  if (![position, target, fromOrigin, toOrigin].every(finiteVector)) {
    throw new RangeError('Expected finite 3D vectors');
  }
  const shift = fromOrigin.map((value, index) => value - toOrigin[index]);
  return {
    position: position.map((value, index) => value + shift[index]),
    target: target.map((value, index) => value + shift[index])
  };
}

export function semanticDecision(
  sample: SemanticSample,
  thresholds: SemanticThresholds
): 'enter' | 'exit' | null {
  if (![sample.candidateDistance, sample.stableMs, sample.contextDistance,
        thresholds.enterDistance, thresholds.exitDistance, thresholds.dwellMs].every(Number.isFinite)
      || thresholds.enterDistance <= 0
      || thresholds.exitDistance <= thresholds.enterDistance
      || thresholds.dwellMs < 0) {
    throw new RangeError('Invalid semantic zoom thresholds or sample');
  }
  if (sample.candidateEligible
      && sample.stableMs >= thresholds.dwellMs
      && sample.candidateDistance <= thresholds.enterDistance) {
    return 'enter';
  }
  if (sample.canExit && sample.contextDistance >= thresholds.exitDistance) {
    return 'exit';
  }
  return null;
}

export function transitionScalar(from: number, to: number, progress: number): number {
  if (![from, to, progress].every(Number.isFinite)) throw new RangeError('Expected finite transition values');
  const t = Math.min(1, Math.max(0, progress));
  const eased = t * t * (3 - 2 * t);
  return from + (to - from) * eased;
}

export function shouldAcceptProjection(
  latestRequestId: string,
  currentMapId: string | null,
  currentRevision: number,
  incoming: {requestId: string; mapId: string; revision: number}
): boolean {
  if (!incoming.requestId || !incoming.mapId || !Number.isFinite(incoming.revision)) return false;
  if (incoming.requestId !== latestRequestId) return false;
  if (currentMapId !== null && incoming.mapId !== currentMapId) return false;
  return incoming.revision >= currentRevision;
}
