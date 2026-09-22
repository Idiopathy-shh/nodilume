export type NavigateToPlacement = {
  mapId: string;
  revision: number;
  placementId: string;
  openContextPlacementId: string | null;
};

export function acceptsSearchNavigation(
  activeMapId: string | null,
  activeRevision: number,
  hasPendingRequest: boolean,
  command: NavigateToPlacement
): boolean {
  return !hasPendingRequest
    && typeof command.mapId === 'string'
    && command.mapId === activeMapId
    && Number.isSafeInteger(command.revision)
    && command.revision === activeRevision
    && typeof command.placementId === 'string'
    && command.placementId.length > 0
    && (command.openContextPlacementId === null
      || typeof command.openContextPlacementId === 'string');
}