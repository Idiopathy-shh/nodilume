import test from 'node:test';
import assert from 'node:assert/strict';
import { localDragPosition, acceptsEditResult } from '../src/editing.ts';

test('drag persists local displacement independently of the scene origin', () => {
  assert.deepEqual(localDragPosition([3,4,5], [1003,-996,205], [1013,-976,200]), [13,24,0]);
  assert.deepEqual(localDragPosition([3,4,5], [3,4,5], [13,24,0]), [13,24,0]);
});
test('zero drag remains a no-op; inverse displacement restores all axes', () => {
  assert.deepEqual(localDragPosition([1,2,3], [40,50,60], [40,50,60]), [1,2,3]);
  const moved = localDragPosition([1,2,3], [40,50,60], [41,48,66]);
  assert.deepEqual(localDragPosition(moved, [41,48,66], [40,50,60]), [1,2,3]);
});
test('invalid or overflowing drag geometry is rejected before a command', () => {
  assert.throws(() => localDragPosition([1,2], [0,0,0], [1,1,1]), RangeError);
  assert.throws(() => localDragPosition([1,2,3], [0,NaN,0], [1,1,1]), RangeError);
  assert.throws(() => localDragPosition([Number.MAX_VALUE,0,0], [0,0,0], [Number.MAX_VALUE,0,0]), RangeError);
});
test('only the pending command on the active map can release editing state', () => {
  assert.equal(acceptsEditResult('new','map',{requestId:'old',mapId:'map'}), false);
  assert.equal(acceptsEditResult('new','map',{requestId:'new',mapId:'other'}), false);
  assert.equal(acceptsEditResult(null,'map',{requestId:'new',mapId:'map'}), false);
  assert.equal(acceptsEditResult('new','map',{requestId:'new',mapId:'map'}), true);
});
