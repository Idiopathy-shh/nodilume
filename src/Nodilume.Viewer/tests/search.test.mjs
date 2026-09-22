import test from 'node:test';
import assert from 'node:assert/strict';
import { acceptsSearchNavigation } from '../src/search.ts';

const command = {
  mapId: 'map-a',
  revision: 7,
  placementId: 'placement-a',
  openContextPlacementId: 'parent-a'
};

test('search navigation accepts only the active idle map revision', () => {
  assert.equal(acceptsSearchNavigation('map-a', 7, false, command), true);
  assert.equal(acceptsSearchNavigation('map-b', 7, false, command), false);
  assert.equal(acceptsSearchNavigation(null, 7, false, command), false);
  assert.equal(acceptsSearchNavigation('map-a', 8, false, command), false);
  assert.equal(acceptsSearchNavigation('map-a', 7, true, command), false);
  assert.equal(acceptsSearchNavigation('map-a', 7, false,
    {...command, placementId: ''}), false);
});