import test from 'node:test';
import assert from 'node:assert/strict';
import { focusPose } from '../src/navigation.ts';

test('focus preserves viewing direction and reaches the requested distance', () => {
  const pose = focusPose([0, 0, 100], [0, 0, 0], [20, 30, 40], 25);
  assert.deepEqual(pose.target, [20, 30, 40]);
  assert.deepEqual(pose.position, [20, 30, 65]);
});
test('coincident camera and target produce finite coordinates', () => {
  const pose = focusPose([0, 0, 0], [0, 0, 0], [1, 2, 3], 10);
  assert.ok(pose.position.every(Number.isFinite));
  assert.equal(Math.hypot(...pose.position.map((v, i) => v - pose.target[i])), 10);
});
test('focus accepts diagonal views without changing distance or input vectors', () => {
  const node = [9, -4, 2];
  const pose = focusPose([10, 10, 10], [0, 0, 0], node, 30);
  assert.ok(Math.abs(Math.hypot(...pose.position.map((v,i) => v-node[i])) - 30) < 1e-10);
  assert.deepEqual(node, [9, -4, 2]);
});
test('invalid geometry is rejected before it reaches the camera', () => {
  for (const distance of [0, -1, NaN, Infinity]) {
    assert.throws(() => focusPose([0,0,1], [0,0,0], [0,0,0], distance), RangeError);
  }
  assert.throws(() => focusPose([NaN,0,1], [0,0,0], [0,0,0], 10), RangeError);
});
