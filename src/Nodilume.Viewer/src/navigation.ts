export function focusPose(position: number[], target: number[], node: number[], distance: number): {position: number[], target: number[]} {
  if (![position, target, node].every(v => v.length === 3 && v.every(Number.isFinite)) || !Number.isFinite(distance) || distance <= 0) {
    throw new RangeError('Expected finite 3D vectors and a positive distance');
  }
  const direction = position.map((v, i) => v - target[i]);
  const length = Math.hypot(...direction);
  const unit = length > 1e-8 ? direction.map(v => v / length) : [0, 0, 1];
  return { position: node.map((v, i) => v + unit[i] * distance), target: [...node] };
}
