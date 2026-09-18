import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { focusPose } from './navigation';
import './styles.css';

type Node = { id: string; title: string; x: number; y: number; z: number; color: string };
type SceneMessage = { version: number; type: string; mapId: string; revision: number; nodes: Node[]; links: { source: string; target: string }[] };
type Bridge = { postMessage: (message: unknown) => void; addEventListener: (type: string, handler: (e: {data: SceneMessage}) => void) => void };
const bridge = (window as unknown as {chrome?: {webview?: Bridge}}).chrome?.webview;
const el = <T extends HTMLElement = HTMLElement>(id: string) => document.getElementById(id) as T;
function fail(error: unknown) {
  el('error').hidden = false;
  el('error').textContent = 'La vista 3D non è disponibile. Verifica il supporto WebGL e riapri Nodilume.';
  document.body.dataset.state = 'error';
  bridge?.postMessage({version: 1, type: 'error'});
  console.error(error);
}
window.addEventListener('error', e => fail(e.error));

try {
  const renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.setClearColor(0x0a111b);
  el('scene').append(renderer.domElement);
  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(48, 1, 0.1, 4000);
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.dampingFactor = 0.09;
  controls.minDistance = 3;
  controls.maxDistance = 1500;
  scene.add(new THREE.AmbientLight(0xffffff, 2));
  const light = new THREE.DirectionalLight(0xffffff, 3);
  light.position.set(50, 100, 180);
  scene.add(light);
  const graph = new THREE.Group(); scene.add(graph);
  const geometry = new THREE.SphereGeometry(1, 24, 16);
  const nodes = new Map<string, {data: Node; mesh: THREE.Mesh; label: HTMLButtonElement}>();
  let selected: string | null = null;
  let animation: {start: number; from: THREE.Vector3; fromTarget: THREE.Vector3; to: THREE.Vector3; toTarget: THREE.Vector3} | null = null;
  let revision = -1;
  let rendered = false;
  let reportFrame = false;
  const keys = new Set<string>();
  const forward = new THREE.Vector3(), right = new THREE.Vector3(), movement = new THREE.Vector3();
  function moveTo(position: THREE.Vector3, target: THREE.Vector3) {
    controls.update();
    animation = {start: performance.now(), from: camera.position.clone(), fromTarget: controls.target.clone(), to: position, toTarget: target};
  }
  function home(animate = true) {
    const position = new THREE.Vector3(30, 65, 500), target = new THREE.Vector3(-35, 0, 0);
    if (animate) moveTo(position, target);
    else { camera.position.copy(position); controls.target.copy(target); controls.update(); }
  }
  home(false);
  function select(id: string) {
    selected = id;
    for (const [key, item] of nodes) {
      item.label.classList.toggle('selected', key === id);
      item.mesh.scale.setScalar((key === id ? 1.3 : 1) * (key === 'root' ? 9 : key.endsWith('-0') ? 7 : 4));
    }
    el('selection-title').textContent = nodes.get(id)!.data.title;
    el('selection-help').textContent = 'Avvicinati per osservarne le connessioni.';
    el<HTMLButtonElement>('focus').disabled = false;
  }
  function focus() {
    if (!selected) return;
    const pose = focusPose(camera.position.toArray(), controls.target.toArray(), nodes.get(selected)!.mesh.position.toArray(), 85);
    moveTo(new THREE.Vector3().fromArray(pose.position), new THREE.Vector3().fromArray(pose.target));
  }
  el('home').onclick = () => home();
  el('focus').onclick = focus;
  controls.addEventListener('start', () => animation = null);
  const raycaster = new THREE.Raycaster(), pointer = new THREE.Vector2();
  let downX = 0, downY = 0;
  renderer.domElement.addEventListener('pointerdown', e => { downX = e.clientX; downY = e.clientY; el('scene').focus(); });
  function pick(e: MouseEvent) {
    const rect = renderer.domElement.getBoundingClientRect();
    pointer.set((e.clientX - rect.left) / rect.width * 2 - 1, -(e.clientY - rect.top) / rect.height * 2 + 1);
    raycaster.setFromCamera(pointer, camera);
    return raycaster.intersectObjects([...nodes.values()].map(n => n.mesh))[0]?.object.userData.id as string | undefined;
  }
  renderer.domElement.addEventListener('click', e => {
    if (Math.hypot(e.clientX - downX, e.clientY - downY) > 5) return;
    const id = pick(e); if (id) select(id);
  });
  renderer.domElement.addEventListener('dblclick', e => { const id = pick(e); if (id) {select(id); focus();} });
  window.addEventListener('keydown', e => {
    if (e.ctrlKey || e.altKey || e.metaKey || document.activeElement !== el('scene')) return;
    if (['KeyW','KeyA','KeyS','KeyD','KeyQ','KeyE','ShiftLeft','ShiftRight'].includes(e.code)) { keys.add(e.code); e.preventDefault(); animation = null; }
  });
  window.addEventListener('keyup', e => keys.delete(e.code));
  window.addEventListener('blur', () => keys.clear());
  el('scene').addEventListener('blur', () => keys.clear());
  document.addEventListener('visibilitychange', () => keys.clear());
  const resize = () => { const {width, height} = el('scene').getBoundingClientRect(); camera.aspect = width / Math.max(1,height); camera.updateProjectionMatrix(); renderer.setSize(width, height); };
  new ResizeObserver(resize).observe(el('scene')); resize();

  bridge?.addEventListener('message', ({data}) => {
    if (data.version !== 1 || data.type !== 'scene' || data.mapId !== 'demo' || data.revision <= revision) return;
    revision = data.revision;
    for (const child of [...graph.children]) {
      graph.remove(child);
      if (child instanceof THREE.Mesh || child instanceof THREE.LineSegments) {
        if (child.geometry !== geometry) child.geometry.dispose();
        const materials = Array.isArray(child.material) ? child.material : [child.material];
        materials.forEach(material => material.dispose());
      }
    }
    nodes.clear(); el('labels').replaceChildren(); selected = null;
    el<HTMLButtonElement>('focus').disabled = true;
    el('selection-title').textContent = "Scegli un'idea";
    for (const node of data.nodes) {
      const material = new THREE.MeshStandardMaterial({color: node.color, emissive: node.color, emissiveIntensity: 0.35, roughness: 0.45});
      const mesh = new THREE.Mesh(geometry, material);
      mesh.position.set(node.x, node.y, node.z);
      mesh.scale.setScalar(node.id === 'root' ? 9 : node.id.endsWith('-0') ? 7 : 4);
      mesh.userData.id = node.id; graph.add(mesh);
      const label = document.createElement('button'); label.className = 'node-label'; label.textContent = node.title;
      label.onclick = () => select(node.id); label.ondblclick = () => { select(node.id); focus(); };
      el('labels').append(label); nodes.set(node.id, {data: node, mesh, label});
    }
    const positions: number[] = [];
    for (const link of data.links) {
      const a = nodes.get(link.source), b = nodes.get(link.target);
      if (a && b) positions.push(...a.mesh.position.toArray(), ...b.mesh.position.toArray());
    }
    const edges = new THREE.BufferGeometry(); edges.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    graph.add(new THREE.LineSegments(edges, new THREE.LineBasicMaterial({color: 0x69849e, transparent: true, opacity: 0.4})));
    el('counts').textContent = `${data.nodes.length} nodi · ${data.links.length} connessioni`;
    rendered = true; reportFrame = true;
  });
  const projected = new THREE.Vector3();
  let previousTime = performance.now();
  function frame(now: number) {
    const dt = Math.min((now - previousTime) / 1000, 0.05); previousTime = now;
    if (animation) {
      const t = Math.min((now - animation.start) / 700, 1), eased = t*t*(3-2*t);
      camera.position.lerpVectors(animation.from, animation.to, eased);
      controls.target.lerpVectors(animation.fromTarget, animation.toTarget, eased);
      if (t === 1) animation = null;
    } else if (keys.size) {
      camera.getWorldDirection(forward); right.crossVectors(forward, camera.up).normalize(); movement.set(0,0,0);
      movement.addScaledVector(forward, Number(keys.has('KeyW'))-Number(keys.has('KeyS')));
      movement.addScaledVector(right, Number(keys.has('KeyD'))-Number(keys.has('KeyA')));
      movement.y += Number(keys.has('KeyE'))-Number(keys.has('KeyQ'));
      if (movement.lengthSq()) movement.normalize().multiplyScalar(dt * (keys.has('ShiftLeft') || keys.has('ShiftRight') ? 220 : 80));
      camera.position.add(movement); controls.target.add(movement);
    }
    controls.update(); renderer.render(scene, camera);
    const width = renderer.domElement.clientWidth, height = renderer.domElement.clientHeight;
    for (const node of nodes.values()) {
      projected.copy(node.mesh.position).project(camera);
      node.label.hidden = projected.z < -1 || projected.z > 1 || Math.abs(projected.x)>1.1 || Math.abs(projected.y)>1.1;
      node.label.style.left = `${(projected.x * 0.5 + 0.5) * width}px`;
      node.label.style.top = `${(-projected.y * 0.5 + 0.5) * height + 18}px`;
    }
    if (rendered && reportFrame) { reportFrame = false; document.body.dataset.state = 'ready'; bridge?.postMessage({version:1,type:'rendered',nodeCount:nodes.size,linkCount:27}); }
  }
  renderer.setAnimationLoop(frame);
  renderer.domElement.addEventListener('webglcontextlost', e => {e.preventDefault(); renderer.setAnimationLoop(null); fail('WebGL context lost');});
  if (!bridge) fail('Desktop bridge missing');
  bridge?.postMessage({version:1,type:'ready'});
} catch (error) { fail(error); }
