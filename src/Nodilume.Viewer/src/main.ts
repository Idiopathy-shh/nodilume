import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { focusPose } from './navigation';
import {
  DEFAULT_SEMANTIC_THRESHOLDS,
  reframePose,
  semanticDecision,
  shouldAcceptProjection,
  transitionScalar
} from './semantic';
import './styles.css';

type NodeData = {
  id: string; placementId: string; ideaId: string; parentPlacementId: string | null;
  title: string; x: number; y: number; z: number; color: string; depth: number;
  role: string; showLabel: boolean; hasChildren: boolean; directChildCount: number;
};
type LinkData = {
  id: string; category: 'containment' | 'relation'; source: string; target: string;
  kind: string; isDirected: boolean; count: number; relationIds: string[];
};
type ContextItem = { placementId: string; ideaId: string; title: string; depth: number };
type Candidate = {
  placementId: string; openContextPlacementId: string | null; ideaId: string;
  title: string; path: string;
};
type Endpoint = {
  ideaId: string; title: string; status: string; visiblePlacementId: string | null;
  candidates: Candidate[];
};
type RelationNavigation = {
  relationId: string; kind: string; isDirected: boolean; explanation: string;
  source: Endpoint; target: Endpoint;
};
type PageInfo = {
  afterPlacementId: string | null; nextPlacementId: string | null;
  hasPrevious: boolean; hasMore: boolean; pageItemCount: number;
};
type Projection = {
  version: 2; type: 'projection'; requestId: string; mapId: string; revision: number;
  transferSentUnixMs: number;
  state: 'ready' | 'partial' | 'empty' | 'leaf'; contextPlacementId: string | null;
  parentContextPlacementId: string | null;
  frameOrigin: {x: number; y: number; z: number}; path: ContextItem[]; page: PageInfo;
  nodes: NodeData[]; links: LinkData[]; relations: RelationNavigation[];
  hiddenInternalRelationCount: number; partialReasons: string[];
};
type ProjectionError = {
  version: 2; type: 'projectionError'; requestId: string; mapId: string | null;
  revision: number | null; contextPlacementId: string | null; code: string; message: string;
};
type Incoming = Projection | ProjectionError;
type Bridge = {
  postMessage: (message: unknown) => void;
  addEventListener: (type: string, handler: (e: {data: Incoming}) => void) => void;
};
type VisualNode = {
  data: NodeData;
  mesh: THREE.Mesh<THREE.SphereGeometry, THREE.MeshStandardMaterial>;
  label: HTMLButtonElement | null;
  from: THREE.Vector3; to: THREE.Vector3;
  fromScale: number; toScale: number; fromOpacity: number; toOpacity: number;
  removing: boolean;
};
type VisualLink = {
  data: LinkData;
  line: THREE.Line<THREE.BufferGeometry, THREE.LineBasicMaterial>;
  arrow: THREE.Mesh<THREE.ConeGeometry, THREE.MeshStandardMaterial> | null;
  label: HTMLSpanElement | null;
  fromOpacity: number; toOpacity: number; removing: boolean;
};
type ReturnSnapshot = {
  contextId: string | null; globalCamera: number[]; globalTarget: number[];
  selectedId: string | null;
};
type PendingRequest = {
  requestId: string;
  kind: 'initial' | 'enter' | 'exit' | 'overview' | 'navigate' | 'return' | 'page';
  contextId: string | null; focusPlacementId?: string; restore?: ReturnSnapshot;
  pageAfterPlacementId?: string | null;
};

const bridge = (window as unknown as {chrome?: {webview?: Bridge}}).chrome?.webview;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
const thresholds = DEFAULT_SEMANTIC_THRESHOLDS;
const protocolVersion = 2;
const requestPrefix = Date.now().toString(36);
let requestSequence = 0;
let latestRequestId = '';

function nextRequestId(): string {
  requestSequence += 1;
  return requestPrefix + '-' + requestSequence;
}
function originArray(value: {x: number; y: number; z: number}): number[] {
  return [value.x, value.y, value.z];
}
function finiteProjection(data: Projection): boolean {
  return data.nodes.every(node => [node.x, node.y, node.z].every(Number.isFinite))
    && originArray(data.frameOrigin).every(Number.isFinite);
}
function fail(error: unknown): void {
  el('error').hidden = false;
  el('error').textContent = 'La vista 3D non è disponibile. Verifica il supporto WebGL e riapri Nodilume.';
  document.body.dataset.state = 'error';
  bridge?.postMessage({version: protocolVersion, type: 'error', requestId: latestRequestId || nextRequestId()});
  console.error(error);
}
window.addEventListener('error', event => fail(event.error));

try {
  const renderer = new THREE.WebGLRenderer({antialias: true});
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.setClearColor(0x0a111b);
  el('scene').append(renderer.domElement);

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(48, 1, 0.1, 5000);
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.dampingFactor = 0.09;
  controls.minDistance = 3;
  controls.maxDistance = 1800;
  scene.add(new THREE.AmbientLight(0xffffff, 2));
  const light = new THREE.DirectionalLight(0xffffff, 3);
  light.position.set(50, 100, 180);
  scene.add(light);

  const graph = new THREE.Group();
  scene.add(graph);
  const sphereGeometry = new THREE.SphereGeometry(1, 24, 16);
  const coneGeometry = new THREE.ConeGeometry(2.1, 7, 12);
  const nodes = new Map<string, VisualNode>();
  const links = new Map<string, VisualLink>();

  let activeMapId: string | null = null;
  let revision = -1;
  let projection: Projection | null = null;
  let frameOrigin = [0, 0, 0];
  let selected: string | null = null;
  let selectedSince = 0;
  let selectionRequestedAt: number | null = null;
  const pageStartedAt = performance.now();
  let firstUsefulRecorded = false;
  const frameSamplesMs: number[] = [];
  let frameSampleSequence = 0;
  let hovered: string | null = null;
  let hoveredSince = 0;
  let pending: PendingRequest | null = null;
  let semanticCooldownUntil = 0;
  const returnStack: ReturnSnapshot[] = [];
  const pageCursorHistory = new Map<string, Array<string | null>>();
  let cameraAnimation: {
    start: number; from: THREE.Vector3; fromTarget: THREE.Vector3;
    to: THREE.Vector3; toTarget: THREE.Vector3;
  } | null = null;
  let visualTransition: {start: number; duration: number; requestId: string} | null = null;
  let reportAfterTransition = false;
  let projectionReceivedAt: number | null = null;
  const keys = new Set<string>();
  const forward = new THREE.Vector3();
  const right = new THREE.Vector3();
  const movement = new THREE.Vector3();
  const projectedPoint = new THREE.Vector3();
  const raycaster = new THREE.Raycaster();
  const pointer = new THREE.Vector2();
  let downX = 0;
  let downY = 0;

  function baseScale(data: NodeData): number {
    if (data.role === 'context') return data.hasChildren ? 10 : 5;
    if (data.role === 'ancestor') return 5.5;
    if (data.hasChildren) return data.depth === 0 ? 9 : 7;
    return 4.4;
  }
  function targetOpacity(data: NodeData): number {
    if (data.role === 'context') return data.hasChildren ? 0.18 : 0.55;
    if (data.role === 'ancestor') return 0.16;
    if (data.role === 'sibling') return 0.62;
    return 1;
  }
  function linkOpacity(data: LinkData): number {
    return data.category === 'containment' ? 0.22 : 0.72;
  }
  function moveTo(position: THREE.Vector3, target: THREE.Vector3): void {
    controls.update();
    cameraAnimation = {
      start: performance.now(),
      from: camera.position.clone(),
      fromTarget: controls.target.clone(),
      to: position,
      toTarget: target
    };
  }
  function homeCamera(animate = true): void {
    const position = new THREE.Vector3(30, 65, 500);
    const target = new THREE.Vector3(-35, 0, 0);
    if (animate) moveTo(position, target);
    else {
      camera.position.copy(position);
      controls.target.copy(target);
      controls.update();
    }
  }
  function focusPlacement(id: string, distance = 82): void {
    const item = nodes.get(id);
    if (!item) return;
    const pose = focusPose(
      camera.position.toArray(),
      controls.target.toArray(),
      item.mesh.position.toArray(),
      distance);
    moveTo(new THREE.Vector3().fromArray(pose.position), new THREE.Vector3().fromArray(pose.target));
  }
  homeCamera(false);

  function renderRelations(): void {
    const container = el('relations');
    container.replaceChildren();
    if (!projection || !selected) {
      const text = document.createElement('p');
      text.className = 'muted';
      text.textContent = 'Seleziona un nodo per vedere le destinazioni.';
      container.append(text);
      return;
    }
    const matches = (endpoint: Endpoint): boolean =>
      endpoint.visiblePlacementId === selected
      || endpoint.candidates.some(candidate => candidate.placementId === selected);
    const relevant = projection.relations.filter(relation =>
      matches(relation.source) || matches(relation.target));
    if (relevant.length === 0) {
      const text = document.createElement('p');
      text.className = 'muted';
      text.textContent = 'Nessuna relazione concettuale per questa rappresentazione.';
      container.append(text);
      return;
    }
    for (const relation of relevant) {
      const fromSource = matches(relation.source);
      const endpoint = fromSource ? relation.target : relation.source;
      const card = document.createElement('div');
      card.className = 'relation-card';

      const title = document.createElement('div');
      title.className = 'relation-title';
      const arrow = relation.isDirected ? (fromSource ? '→ ' : '← ') : '↔ ';
      title.textContent = arrow + endpoint.title;
      card.append(title);

      const meta = document.createElement('div');
      meta.className = 'relation-meta';
      meta.textContent = relation.kind + (relation.explanation ? ' · ' + relation.explanation : '');
      card.append(meta);

      if (endpoint.status === 'unplaced' || endpoint.candidates.length === 0) {
        const note = document.createElement('p');
        note.className = 'destination-note';
        note.textContent = 'Idea presente nel modello, ma senza collocazione navigabile.';
        card.append(note);
      } else {
        if (endpoint.candidates.length > 1) {
          const note = document.createElement('p');
          note.className = 'destination-note';
          note.textContent = 'Scegli la collocazione:';
          card.append(note);
        }
        for (const candidate of endpoint.candidates) {
          const button = document.createElement('button');
          button.className = 'destination';
          button.textContent = candidate.path;
          button.onclick = () => navigateCandidate(candidate);
          card.append(button);
        }
      }
      container.append(card);
    }
  }

  function updateSelectionUI(): void {
    const item = selected ? nodes.get(selected) : null;
    el<HTMLButtonElement>('focus').disabled = !item;
    el<HTMLButtonElement>('enter').disabled = !item?.data.hasChildren;
    if (!item) {
      el('selection-title').textContent = "Scegli un'idea";
      el('selection-help').textContent =
        'Un clic seleziona. Un doppio clic entra nei gruppi o mette a fuoco una foglia.';
    } else {
      el('selection-title').textContent = item.data.title;
      el('selection-help').textContent = item.data.hasChildren
        ? item.data.directChildCount + ' elementi diretti · Entra per aprire questo contesto.'
        : 'Foglia · nessun livello interno da aprire.';
    }
    renderRelations();
  }

  function select(id: string): void {
    if (!nodes.has(id)) return;
    selected = id;
    selectedSince = performance.now();
    selectionRequestedAt = selectedSince;
    for (const [key, item] of nodes)
      item.label?.classList.toggle('selected', key === id);
    updateSelectionUI();
    bridge?.postMessage({version: protocolVersion, type: 'selectionChanged',
      requestId: nextRequestId(), mapId: activeMapId, revision,
      placementId: nodes.get(id)?.data.placementId ?? null});
  }
  function clearSelection(): void {
    selected = null;
    for (const item of nodes.values()) item.label?.classList.remove('selected');
    updateSelectionUI();
    bridge?.postMessage({version: protocolVersion, type: 'selectionChanged',
      requestId: nextRequestId(), mapId: activeMapId, revision, placementId: null});
  }
  function snapshotReturn(): ReturnSnapshot {
    return {
      contextId: projection?.contextPlacementId ?? null,
      globalCamera: camera.position.toArray().map((value, index) => value + frameOrigin[index]),
      globalTarget: controls.target.toArray().map((value, index) => value + frameOrigin[index]),
      selectedId: selected
    };
  }
  function updateNavigationButtons(): void {
    el<HTMLButtonElement>('up').disabled = !projection?.parentContextPlacementId || pending !== null;
    el<HTMLButtonElement>('back').disabled = returnStack.length === 0 || pending !== null;
    el<HTMLButtonElement>('page-prev').disabled = !projection?.page.hasPrevious || pending !== null;
    el<HTMLButtonElement>('page-next').disabled = !projection?.page.hasMore || pending !== null;
  }
  function setLoading(text = 'Caricamento…'): void {
    el('projection-status').textContent = text;
    document.body.dataset.loadState = 'loading';
    updateNavigationButtons();
  }
  function setProjectionStatus(data: Projection): void {
    document.body.dataset.loadState = data.state;
    if (data.state === 'partial') {
      el('projection-status').textContent = 'Parziale · ' + data.partialReasons.length + ' limiti';
    } else if (data.state === 'leaf') {
      el('projection-status').textContent = 'Foglia · nessun livello interno';
    } else if (data.state === 'empty') {
      el('projection-status').textContent = 'Contesto vuoto';
    } else {
      el('projection-status').textContent = 'Contesto pronto';
    }
  }
  function requestProjection(
    contextId: string | null,
    kind: PendingRequest['kind'],
    options: Pick<PendingRequest, 'focusPlacementId' | 'restore' | 'pageAfterPlacementId'> = {}
  ): void {
    const requestId = nextRequestId();
    latestRequestId = requestId;
    pending = {requestId, kind, contextId, ...options};
    document.body.dataset.requestId = requestId;
    document.body.dataset.requestContext = contextId ?? '';
    setLoading(kind === 'page' ? 'Caricamento pagina…' : 'Caricamento…');
    bridge?.postMessage({
      version: protocolVersion,
      type: 'projectionRequest',
      requestId,
      mapId: activeMapId,
      revision: revision >= 0 ? revision : null,
      context: {placementId: contextId},
      page: {afterPlacementId: options.pageAfterPlacementId ?? null},
      selection: {placementId: selected}
    });
  }
  function enterSelected(): void {
    if (!selected) return;
    const item = nodes.get(selected);
    if (!item) return;
    if (!item.data.hasChildren) {
      focusPlacement(selected);
      return;
    }
    requestProjection(item.data.placementId, 'enter');
  }
  function exitContext(): void {
    if (!projection?.parentContextPlacementId) return;
    requestProjection(projection.parentContextPlacementId, 'exit');
  }
  function overview(): void {
    clearSelection();
    requestProjection(null, 'overview');
  }
  function returnToPrevious(): void {
    const restore = returnStack.pop();
    if (!restore) return;
    requestProjection(restore.contextId, 'return', {restore});
  }
  function pageKey(): string {
    return projection?.contextPlacementId ?? '__root__';
  }
  function nextPage(): void {
    if (!projection?.page.hasMore || !projection.page.nextPlacementId) return;
    const key = pageKey();
    const history = pageCursorHistory.get(key) ?? [];
    history.push(projection.page.afterPlacementId);
    pageCursorHistory.set(key, history);
    requestProjection(projection.contextPlacementId, 'page', {
      pageAfterPlacementId: projection.page.nextPlacementId
    });
  }
  function previousPage(): void {
    if (!projection?.page.hasPrevious) return;
    const key = pageKey();
    const history = pageCursorHistory.get(key) ?? [];
    const previous = history.length > 0 ? history.pop() ?? null : null;
    pageCursorHistory.set(key, history);
    requestProjection(projection.contextPlacementId, 'page', {
      pageAfterPlacementId: previous
    });
  }
  function navigateCandidate(candidate: Candidate): void {
    returnStack.push(snapshotReturn());
    updateNavigationButtons();
    requestProjection(candidate.openContextPlacementId, 'navigate', {
      focusPlacementId: candidate.placementId
    });
  }

  el<HTMLButtonElement>('focus').onclick = () => {
    if (selected) focusPlacement(selected);
  };
  el<HTMLButtonElement>('enter').onclick = enterSelected;
  el<HTMLButtonElement>('up').onclick = exitContext;
  el<HTMLButtonElement>('home').onclick = overview;
  el<HTMLButtonElement>('back').onclick = returnToPrevious;
  el<HTMLButtonElement>('page-prev').onclick = previousPage;
  el<HTMLButtonElement>('page-next').onclick = nextPage;

  function pick(clientX: number, clientY: number): string | undefined {
    const rect = renderer.domElement.getBoundingClientRect();
    pointer.set(
      (clientX - rect.left) / rect.width * 2 - 1,
      -(clientY - rect.top) / rect.height * 2 + 1);
    raycaster.setFromCamera(pointer, camera);
    return raycaster.intersectObjects(
      [...nodes.values()].filter(item => !item.removing).map(item => item.mesh)
    )[0]?.object.userData.id as string | undefined;
  }

  renderer.domElement.addEventListener('pointerdown', event => {
    downX = event.clientX;
    downY = event.clientY;
    el('scene').focus();
  });
  renderer.domElement.addEventListener('pointermove', event => {
    const id = pick(event.clientX, event.clientY) ?? null;
    if (id !== hovered) {
      hovered = id;
      hoveredSince = performance.now();
    }
  });
  renderer.domElement.addEventListener('pointerleave', () => {
    hovered = null;
    hoveredSince = 0;
  });
  renderer.domElement.addEventListener('click', event => {
    if (Math.hypot(event.clientX - downX, event.clientY - downY) > 5) return;
    const id = pick(event.clientX, event.clientY);
    if (id) select(id);
  });
  renderer.domElement.addEventListener('dblclick', event => {
    const id = pick(event.clientX, event.clientY);
    if (!id) return;
    select(id);
    const item = nodes.get(id);
    if (item?.data.hasChildren) enterSelected();
    else focusPlacement(id);
  });
  controls.addEventListener('start', () => cameraAnimation = null);

  window.addEventListener('keydown', event => {
    if (event.ctrlKey || event.altKey || event.metaKey || document.activeElement !== el('scene')) return;
    if (event.code === 'Enter') {
      enterSelected();
      event.preventDefault();
      return;
    }
    if (event.code === 'Escape') {
      exitContext();
      event.preventDefault();
      return;
    }
    if (['KeyW','KeyA','KeyS','KeyD','KeyQ','KeyE','ShiftLeft','ShiftRight'].includes(event.code)) {
      keys.add(event.code);
      event.preventDefault();
      cameraAnimation = null;
    }
  });
  window.addEventListener('keyup', event => keys.delete(event.code));
  window.addEventListener('blur', () => keys.clear());
  el('scene').addEventListener('blur', () => keys.clear());
  document.addEventListener('visibilitychange', () => keys.clear());

  function createNodeLabel(data: NodeData): HTMLButtonElement {
    const label = document.createElement('button');
    label.className = 'node-label';
    label.textContent = data.title;
    label.onclick = () => select(data.id);
    label.ondblclick = () => {
      select(data.id);
      const current = nodes.get(data.id);
      if (current?.data.hasChildren) enterSelected();
      else focusPlacement(data.id);
    };
    el('labels').append(label);
    return label;
  }

  function createNode(data: NodeData, position: THREE.Vector3): VisualNode {
    const material = new THREE.MeshStandardMaterial({
      color: data.color,
      emissive: data.color,
      emissiveIntensity: 0.35,
      roughness: 0.45,
      transparent: true,
      opacity: 0
    });
    const mesh = new THREE.Mesh(sphereGeometry, material);
    mesh.position.copy(position);
    mesh.scale.setScalar(0.1);
    mesh.userData.id = data.id;
    graph.add(mesh);

    const label = data.showLabel ? createNodeLabel(data) : null;

    return {
      data,
      mesh,
      label,
      from: position.clone(),
      to: new THREE.Vector3(data.x, data.y, data.z),
      fromScale: 0.1,
      toScale: baseScale(data),
      fromOpacity: 0,
      toOpacity: targetOpacity(data),
      removing: false
    };
  }

  function disposeNode(item: VisualNode): void {
    graph.remove(item.mesh);
    item.mesh.material.dispose();
    item.label?.remove();
  }

  function createLink(data: LinkData): VisualLink {
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute([0,0,0,0,0,0], 3));
    const material = new THREE.LineBasicMaterial({
      color: data.category === 'containment' ? 0x51697d : 0x90c9c1,
      transparent: true,
      opacity: 0
    });
    const line = new THREE.Line(geometry, material);
    graph.add(line);

    let arrow: VisualLink['arrow'] = null;
    if (data.category === 'relation' && data.isDirected) {
      const arrowMaterial = new THREE.MeshStandardMaterial({
        color: 0x90c9c1,
        emissive: 0x436b68,
        transparent: true,
        opacity: 0
      });
      arrow = new THREE.Mesh(coneGeometry, arrowMaterial);
      graph.add(arrow);
    }

    let label: HTMLSpanElement | null = null;
    if (data.category === 'relation' && data.count > 1) {
      label = document.createElement('span');
      label.className = 'link-count';
      label.textContent = data.kind + ' ×' + data.count;
      el('link-labels').append(label);
    }

    return {
      data,
      line,
      arrow,
      label,
      fromOpacity: 0,
      toOpacity: linkOpacity(data),
      removing: false
    };
  }

  function disposeLink(item: VisualLink): void {
    graph.remove(item.line);
    item.line.geometry.dispose();
    item.line.material.dispose();
    if (item.arrow) {
      graph.remove(item.arrow);
      item.arrow.material.dispose();
    }
    item.label?.remove();
  }

  function currentTransitionScalar(now: number): number {
    if (!visualTransition) return 1;
    return Math.min(1, Math.max(0, (now - visualTransition.start) / visualTransition.duration));
  }

  function sampleTransition(now: number): void {
    const scalar = currentTransitionScalar(now);
    for (const [id, item] of nodes) {
      item.mesh.position.set(
        transitionScalar(item.from.x, item.to.x, scalar),
        transitionScalar(item.from.y, item.to.y, scalar),
        transitionScalar(item.from.z, item.to.z, scalar));
      const scale = transitionScalar(item.fromScale, item.toScale, scalar);
      item.mesh.scale.setScalar(scale * (selected === id ? 1.25 : 1));
      item.mesh.material.opacity = transitionScalar(item.fromOpacity, item.toOpacity, scalar);
    }
    for (const item of links.values()) {
      const opacity = transitionScalar(item.fromOpacity, item.toOpacity, scalar);
      item.line.material.opacity = opacity;
      if (item.arrow) item.arrow.material.opacity = opacity;
    }
  }

  function reframeExistingVisuals(oldOrigin: number[], newOrigin: number[]): void {
    const shift = oldOrigin.map((value, index) => value - newOrigin[index]);
    const shiftVector = new THREE.Vector3().fromArray(shift);
    for (const item of nodes.values()) item.mesh.position.add(shiftVector);

    const pose = reframePose(
      camera.position.toArray(),
      controls.target.toArray(),
      oldOrigin,
      newOrigin);
    camera.position.fromArray(pose.position);
    controls.target.fromArray(pose.target);
    controls.update();
  }

  function collapsePointFor(data: Projection): THREE.Vector3 {
    if (data.contextPlacementId) {
      const context = data.nodes.find(node => node.placementId === data.contextPlacementId);
      if (context) return new THREE.Vector3(context.x, context.y, context.z);
    }
    return new THREE.Vector3();
  }
  function applyProjection(data: Projection): void {
    if (!finiteProjection(data)) throw new RangeError('Projection contains non-finite geometry.');
    const now = performance.now();
    sampleTransition(now);

    const oldOrigin = [...frameOrigin];
    const newOrigin = originArray(data.frameOrigin);
    reframeExistingVisuals(oldOrigin, newOrigin);
    frameOrigin = newOrigin;

    const collapse = collapsePointFor(data);
    const incomingIds = new Set(data.nodes.map(node => node.id));

    for (const [id, item] of nodes) {
      item.from.copy(item.mesh.position);
      item.fromScale = item.mesh.scale.x / (selected === id ? 1.25 : 1);
      item.fromOpacity = item.mesh.material.opacity;
      if (!incomingIds.has(id)) {
        item.removing = true;
        item.to.copy(collapse);
        item.toScale = 0.1;
        item.toOpacity = 0;
      }
    }

    for (const node of data.nodes) {
      const target = new THREE.Vector3(node.x, node.y, node.z);
      let item = nodes.get(node.id);
      if (!item) {
        item = createNode(node, collapse.clone());
        nodes.set(node.id, item);
      } else {
        item.data = node;
        if (node.showLabel && !item.label) item.label = createNodeLabel(node);
        if (!node.showLabel && item.label) {
          item.label.remove();
          item.label = null;
        }
        if (item.label) item.label.textContent = node.title;
      }
      item.removing = false;
      item.from.copy(item.mesh.position);
      item.to.copy(target);
      item.fromScale = item.mesh.scale.x / (selected === node.id ? 1.25 : 1);
      item.toScale = baseScale(node);
      item.fromOpacity = item.mesh.material.opacity;
      item.toOpacity = targetOpacity(node);
      item.mesh.material.color.set(node.color);
      item.mesh.material.emissive.set(node.color);
    }

    const incomingLinkIds = new Set(data.links.map(link => link.id));
    for (const [id, item] of links) {
      item.fromOpacity = item.line.material.opacity;
      if (!incomingLinkIds.has(id)) {
        item.removing = true;
        item.toOpacity = 0;
      }
    }

    for (const link of data.links) {
      let item = links.get(link.id);
      if (!item) {
        item = createLink(link);
        links.set(link.id, item);
      } else {
        item.data = link;
        if (item.label) item.label.textContent = link.kind + ' ×' + link.count;
      }
      item.removing = false;
      item.fromOpacity = item.line.material.opacity;
      item.toOpacity = linkOpacity(link);
    }

    projection = data;
    activeMapId = data.mapId;
    revision = data.revision;
    document.body.dataset.pageAfter = data.page.afterPlacementId ?? '';
    document.body.dataset.pageHasMore = String(data.page.hasMore);

    if (selected && !incomingIds.has(selected)) clearSelection();
    renderBreadcrumbs();
    setProjectionStatus(data);
    updateSelectionUI();
    updateCounts();

    visualTransition = {start: now, duration: 650, requestId: data.requestId};
    reportAfterTransition = true;
    semanticCooldownUntil = now + 420;
    document.body.dataset.state = 'transitioning';
    updateNavigationButtons();
  }

  function restoreCamera(restore: ReturnSnapshot): void {
    const localPosition = restore.globalCamera.map((value, index) => value - frameOrigin[index]);
    const localTarget = restore.globalTarget.map((value, index) => value - frameOrigin[index]);
    moveTo(
      new THREE.Vector3().fromArray(localPosition),
      new THREE.Vector3().fromArray(localTarget));
    if (restore.selectedId && nodes.has(restore.selectedId)) select(restore.selectedId);
  }

  function finishTransition(now: number): void {
    if (!visualTransition || currentTransitionScalar(now) < 1) return;
    sampleTransition(now);

    for (const [id, item] of [...nodes]) {
      if (!item.removing) continue;
      disposeNode(item);
      nodes.delete(id);
    }
    for (const [id, item] of [...links]) {
      if (!item.removing) continue;
      disposeLink(item);
      links.delete(id);
    }

    visualTransition = null;
    document.body.dataset.state = 'ready';
    if (projectionReceivedAt !== null) {
      document.body.dataset.viewerRenderMs = (now - projectionReceivedAt).toFixed(2);
      projectionReceivedAt = null;
    }
    if (!firstUsefulRecorded) {
      firstUsefulRecorded = true;
      document.body.dataset.firstUsefulMs = (now - pageStartedAt).toFixed(2);
    }

    const completed = pending;
    pending = null;
    if (completed?.restore) {
      restoreCamera(completed.restore);
      semanticCooldownUntil = now + 900;
    } else if (completed?.focusPlacementId && nodes.has(completed.focusPlacementId)) {
      select(completed.focusPlacementId);
      focusPlacement(completed.focusPlacementId);
      semanticCooldownUntil = now + 1000;
    } else if (completed?.kind === 'enter' && projection?.contextPlacementId
        && nodes.has(projection.contextPlacementId)) {
      focusPlacement(projection.contextPlacementId, 105);
      semanticCooldownUntil = now + 1000;
    } else if (completed?.kind === 'overview') {
      homeCamera();
      semanticCooldownUntil = now + 900;
    } else {
      semanticCooldownUntil = now + 650;
    }

    updateNavigationButtons();
    if (reportAfterTransition && projection) {
      reportAfterTransition = false;
      bridge?.postMessage({
        version: protocolVersion,
        type: 'rendered',
        requestId: projection.requestId,
        mapId: projection.mapId,
        revision: projection.revision,
        context: {placementId: projection.contextPlacementId},
        state: projection.state,
        nodeCount: projection.nodes.length,
        linkCount: projection.links.length
      });
    }
  }

  function renderBreadcrumbs(): void {
    const container = el('breadcrumbs');
    container.replaceChildren();
    if (!projection) return;

    const overviewButton = document.createElement('button');
    overviewButton.textContent = 'Panoramica';
    overviewButton.onclick = overview;
    container.append(overviewButton);

    for (const item of projection.path) {
      const separator = document.createElement('span');
      separator.textContent = '›';
      container.append(separator);

      const button = document.createElement('button');
      button.textContent = item.title;
      button.className = item.placementId === projection.contextPlacementId ? 'current' : '';
      button.disabled = item.placementId === projection.contextPlacementId;
      button.onclick = () => requestProjection(item.placementId, 'exit');
      container.append(button);
    }
  }

  function updateCounts(): void {
    if (!projection) return;
    const containment = projection.links.filter(link => link.category === 'containment').length;
    const conceptual = projection.links
      .filter(link => link.category === 'relation')
      .reduce((sum, link) => sum + link.count, 0);
    let text = projection.nodes.length + ' nodi · '
      + containment + ' contenimenti · '
      + conceptual + ' relazioni';
    if (projection.hiddenInternalRelationCount)
      text += ' · ' + projection.hiddenInternalRelationCount + ' interne aggregate';
    el('counts').textContent = text;
  }
  function updateLinkGeometry(item: VisualLink): void {
    const source = nodes.get(item.data.source);
    const target = nodes.get(item.data.target);
    if (!source || !target) {
      item.line.visible = false;
      if (item.arrow) item.arrow.visible = false;
      if (item.label) item.label.hidden = true;
      return;
    }

    item.line.visible = true;
    const positions = item.line.geometry.getAttribute('position') as THREE.BufferAttribute;
    positions.setXYZ(0, source.mesh.position.x, source.mesh.position.y, source.mesh.position.z);
    positions.setXYZ(1, target.mesh.position.x, target.mesh.position.y, target.mesh.position.z);
    positions.needsUpdate = true;

    if (item.arrow) {
      const direction = new THREE.Vector3().subVectors(target.mesh.position, source.mesh.position);
      const length = direction.length();
      if (length > 1e-6) {
        item.arrow.visible = true;
        item.arrow.position.copy(source.mesh.position).addScaledVector(direction, 0.72);
        item.arrow.quaternion.setFromUnitVectors(
          new THREE.Vector3(0, 1, 0),
          direction.normalize());
      } else {
        item.arrow.visible = false;
      }
    }

    if (item.label) {
      projectedPoint.copy(source.mesh.position).lerp(target.mesh.position, 0.5).project(camera);
      const visible = projectedPoint.z >= -1 && projectedPoint.z <= 1
        && Math.abs(projectedPoint.x) <= 1.1
        && Math.abs(projectedPoint.y) <= 1.1;
      item.label.hidden = !visible;
      if (visible) {
        item.label.style.left =
          ((projectedPoint.x * 0.5 + 0.5) * renderer.domElement.clientWidth) + 'px';
        item.label.style.top =
          ((-projectedPoint.y * 0.5 + 0.5) * renderer.domElement.clientHeight) + 'px';
      }
    }
  }

  function updateLabels(): void {
    const width = renderer.domElement.clientWidth;
    const height = renderer.domElement.clientHeight;
    for (const item of nodes.values()) {
      if (!item.label) continue;
      projectedPoint.copy(item.mesh.position).project(camera);
      const hidden = item.removing
        || item.mesh.material.opacity < 0.16
        || projectedPoint.z < -1
        || projectedPoint.z > 1
        || Math.abs(projectedPoint.x) > 1.1
        || Math.abs(projectedPoint.y) > 1.1;
      item.label.hidden = hidden;
      if (!hidden) {
        item.label.style.left = ((projectedPoint.x * 0.5 + 0.5) * width) + 'px';
        item.label.style.top = ((-projectedPoint.y * 0.5 + 0.5) * height + 18) + 'px';
      }
    }
  }

  function semanticCandidate(now: number): {item: VisualNode; stableMs: number} | null {
    const selectedItem = selected ? nodes.get(selected) : null;
    if (selectedItem?.data.hasChildren
        && selectedItem.data.placementId !== projection?.contextPlacementId
        && !selectedItem.removing) {
      return {item: selectedItem, stableMs: now - selectedSince + thresholds.dwellMs};
    }

    const hoveredItem = hovered ? nodes.get(hovered) : null;
    if (hoveredItem?.data.hasChildren
        && hoveredItem.data.placementId !== projection?.contextPlacementId
        && !hoveredItem.removing) {
      return {item: hoveredItem, stableMs: now - hoveredSince};
    }
    return null;
  }

  function runSemanticZoom(now: number): void {
    if (!projection || pending || visualTransition || now < semanticCooldownUntil) return;
    const candidate = semanticCandidate(now);
    const decision = semanticDecision({
      candidateEligible: candidate !== null,
      candidateDistance: candidate
        ? camera.position.distanceTo(candidate.item.mesh.position)
        : Number.MAX_SAFE_INTEGER,
      stableMs: candidate?.stableMs ?? 0,
      canExit: projection.parentContextPlacementId !== null,
      contextDistance: camera.position.length()
    }, thresholds);

    if (decision === 'enter' && candidate) {
      requestProjection(candidate.item.data.placementId, 'enter');
    } else if (decision === 'exit' && projection.parentContextPlacementId) {
      requestProjection(projection.parentContextPlacementId, 'exit');
    }
  }

  bridge?.addEventListener('message', ({data}) => {
    if (!data || data.version !== protocolVersion) return;

    if ((data as {type: string}).type === 'refreshProjection') {
      const refresh = data as unknown as {mapId: string; revision: number};
      if (refresh.mapId === activeMapId && refresh.revision >= revision && !pending)
        requestProjection(projection?.contextPlacementId ?? null, 'page',
          {pageAfterPlacementId: null});
      return;
    }

    if (data.type === 'projectionError') {
      if (data.requestId !== latestRequestId) return;
      pending = null;
      document.body.dataset.loadState = 'error';
      el('projection-status').textContent = 'Errore · ' + data.code;
      updateNavigationButtons();
      return;
    }

    if (data.type !== 'projection') return;
    document.body.dataset.responseRequestId = data.requestId;
    document.body.dataset.responseContext = data.contextPlacementId ?? '';
    if (!shouldAcceptProjection(latestRequestId, activeMapId, revision, data)) return;

    projectionReceivedAt = performance.now();
    document.body.dataset.bridgeTransferMs =
      Math.max(0, Date.now() - data.transferSentUnixMs).toFixed(2);
    try {
      applyProjection(data);
    } catch (error) {
      fail(error);
    }
  });

  const resize = (): void => {
    const bounds = el('scene').getBoundingClientRect();
    camera.aspect = bounds.width / Math.max(1, bounds.height);
    camera.updateProjectionMatrix();
    renderer.setSize(bounds.width, bounds.height);
  };
  new ResizeObserver(resize).observe(el('scene'));
  resize();

  let previousTime = performance.now();
  function frame(now: number): void {
    const rawFrameMs = now - previousTime;
    const dt = Math.min(rawFrameMs / 1000, 0.05);
    previousTime = now;

    if (projection && !visualTransition && document.body.dataset.state === 'ready') {
      frameSamplesMs.push(rawFrameMs);
      if (frameSamplesMs.length > 600) frameSamplesMs.shift();
      frameSampleSequence++;
      if (frameSamplesMs.length >= 30 && frameSampleSequence % 30 === 0) {
        const ordered = [...frameSamplesMs].sort((a, b) => a - b);
        const index = Math.min(ordered.length - 1, Math.ceil(ordered.length * 0.95) - 1);
        document.body.dataset.frameP95Ms = ordered[index].toFixed(2);
        document.body.dataset.frameSamplesMs =
          frameSamplesMs.map(value => value.toFixed(2)).join(',');
      }
    }

    if (cameraAnimation) {
      const t = Math.min((now - cameraAnimation.start) / 700, 1);
      const eased = t * t * (3 - 2 * t);
      camera.position.lerpVectors(cameraAnimation.from, cameraAnimation.to, eased);
      controls.target.lerpVectors(cameraAnimation.fromTarget, cameraAnimation.toTarget, eased);
      if (t === 1) cameraAnimation = null;
    } else if (keys.size) {
      camera.getWorldDirection(forward);
      right.crossVectors(forward, camera.up).normalize();
      movement.set(0, 0, 0);
      movement.addScaledVector(forward, Number(keys.has('KeyW')) - Number(keys.has('KeyS')));
      movement.addScaledVector(right, Number(keys.has('KeyD')) - Number(keys.has('KeyA')));
      movement.y += Number(keys.has('KeyE')) - Number(keys.has('KeyQ'));
      if (movement.lengthSq()) {
        const fast = keys.has('ShiftLeft') || keys.has('ShiftRight');
        movement.normalize().multiplyScalar(dt * (fast ? 220 : 80));
      }
      camera.position.add(movement);
      controls.target.add(movement);
    }

    sampleTransition(now);
    finishTransition(now);
    controls.update();

    for (const item of links.values()) updateLinkGeometry(item);
    renderer.render(scene, camera);
    updateLabels();
    if (selectionRequestedAt !== null) {
      document.body.dataset.selectionLatencyMs = (now - selectionRequestedAt).toFixed(2);
      selectionRequestedAt = null;
    }
    runSemanticZoom(now);
  }
  renderer.setAnimationLoop(frame);

  renderer.domElement.addEventListener('webglcontextlost', event => {
    event.preventDefault();
    renderer.setAnimationLoop(null);
    fail('WebGL context lost');
  });

  if (!bridge) {
    fail('Desktop bridge missing');
  } else {
    latestRequestId = nextRequestId();
    pending = {requestId: latestRequestId, kind: 'initial', contextId: null};
    setLoading();
    bridge.postMessage({
      version: protocolVersion,
      type: 'ready',
      requestId: latestRequestId,
      mapId: null,
      revision: null,
      context: {placementId: null}
    });
  }
} catch (error) {
  fail(error);
}
