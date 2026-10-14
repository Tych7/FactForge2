
export function getPlayerColor(name) {
  if (!name) return "#808080";

  let hash = 0;

  for (let i = 0; i < name.length; i++) {
    // Match C# 32-bit integer overflow.
    hash = (Math.imul(hash, 31) + name.charCodeAt(i)) | 0;
  }

  const hue = (hash >>> 0) % 360;

  // Match PlayerColorHelper.FromHsv(hue, 0.75, 0.9).
  const s = 0.75;
  const v = 0.9;
  const c = v * s;
  const x = c * (1 - Math.abs((hue / 60) % 2 - 1));
  const m = v - c;

  let r = 0, g = 0, b = 0;

  if (hue < 60) [r, g, b] = [c, x, 0];
  else if (hue < 120) [r, g, b] = [x, c, 0];
  else if (hue < 180) [r, g, b] = [0, c, x];
  else if (hue < 240) [r, g, b] = [0, x, c];
  else if (hue < 300) [r, g, b] = [x, 0, c];
  else [r, g, b] = [c, 0, x];

  const toHex = value =>
    Math.floor((value + m) * 255)
      .toString(16)
      .padStart(2, "0");

  return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
}

export function showPlayerAvatar(name) {
  if (!name) return;

  const header = document.getElementById("player-header");
  const avatar = document.getElementById("player-avatar");
  const displayName = document.getElementById("player-display-name");

  avatar.textContent = name.trim().charAt(0).toUpperCase();
  avatar.style.backgroundColor = getPlayerColor(name);
  displayName.textContent = name;

  header.classList.remove("hidden");
}
