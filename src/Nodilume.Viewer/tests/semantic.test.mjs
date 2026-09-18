import test from 'node:test';
import assert from 'node:assert/strict';
import {
  reframePose,
  semanticDecision,
  transitionScalar,
  shouldAcceptProjection
} from '../src/semantic.ts';

test('camera reframing is finite and exactly reversible', () => {
  const start = {position: [30, 40, 90], target: [5, 6, 7]};
  const entered = reframePose(start.position, start.target, [0, 0, 0], [120, -20, 15]);
  const exited = reframePose(entered.position, entered.target, [120, -20, 15], [0, 0, 0]);
  assert.deepEqual(exited, start);
  assert.ok([...entered.position, ...entered.target].every(Number.isFinite));
});

test('semantic hysteresis requires stable candidate and separates enter from exit', () => {
  const thresholds = {enterDistance: 60, exitDistance: 180, dwellMs: 250};
  assert.equal(semanticDecision({
    candidateEligible: true, candidateDistance: 50, stableMs: 100,
    canExit: false, contextDistance: 50
  }, thresholds), null);
  assert.equal(semanticDecision({
    candidateEligible: true, candidateDistance: 50, stableMs: 300,
    canExit: false, contextDistance: 50
  }, thresholds), 'enter');
  assert.equal(semanticDecision({
    candidateEligible: false, candidateDistance: 20, stableMs: 1000,
    canExit: true, contextDistance: 200
  }, thresholds), 'exit');
  assert.equal(semanticDecision({
    candidateEligible: false, candidateDistance: 100, stableMs: 1000,
    canExit: true, contextDistance: 100
  }, thresholds), null);
});

test('transition reversal begins from the current intermediate value', () => {
  const middle = transitionScalar(0, 1, 0.5);
  assert.ok(middle > 0 && middle < 1);
  assert.equal(transitionScalar(middle, 0, 0), middle);
  assert.equal(transitionScalar(middle, 0, 1), 0);
});

test('stale request, map and revision responses are rejected', () => {
  const base = {requestId: 'r2', mapId: 'map-a', revision: 4};
  assert.equal(shouldAcceptProjection('r2', 'map-a', 4, base), true);
  assert.equal(shouldAcceptProjection('r3', 'map-a', 4, base), false);
  assert.equal(shouldAcceptProjection('r2', 'map-b', 4, base), false);
  assert.equal(shouldAcceptProjection('r2', 'map-a', 5, base), false);
});
