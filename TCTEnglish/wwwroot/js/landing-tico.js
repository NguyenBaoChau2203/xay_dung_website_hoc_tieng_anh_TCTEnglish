/**
 * Landing Hero Tico controller (ES module).
 * Loads Three.js + GLB only when available and motion is allowed.
 * Falls back to welcome PNG poster otherwise.
 */
const SESSION_KEY = "tct-tico-greeted";
const GLB_URL = "/brand/mascot/3d/tico-welcome.glb";
const CLIP_ORDER = ["peek", "anticipate", "jump", "land", "greet_wave"];
const IDLE_CLIP = "idle";

function qs(sel, root) {
  return (root || document).querySelector(sel);
}

function prefersReducedMotion() {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function isMobileHeroLayout() {
  return window.matchMedia("(max-width: 768px)").matches;
}

function webglAvailable() {
  try {
    const canvas = document.createElement("canvas");
    return !!(
      window.WebGLRenderingContext &&
      (canvas.getContext("webgl") || canvas.getContext("experimental-webgl"))
    );
  } catch {
    return false;
  }
}

function alreadyGreeted() {
  try {
    return window.sessionStorage.getItem(SESSION_KEY) === "1";
  } catch {
    return false;
  }
}

function markGreeted() {
  try {
    window.sessionStorage.setItem(SESSION_KEY, "1");
  } catch {
    /* ignore */
  }
}

function showPosterOnly(host) {
  const poster = qs(".landing-tico__poster", host);
  const canvas = qs(".landing-tico__canvas", host);
  const bubble = qs(".landing-tico__bubble", host);
  if (poster) poster.hidden = false;
  if (canvas) {
    canvas.hidden = true;
    canvas.setAttribute("aria-hidden", "true");
  }
  if (bubble) bubble.hidden = true;
  host.classList.remove("is-tico-3d");
  host.classList.add("is-tico-poster");
}

function showBubble(host) {
  const bubble = qs(".landing-tico__bubble", host);
  if (!bubble) return;
  bubble.hidden = false;
  window.setTimeout(() => bubble.classList.add("is-visible"), 20);
}

async function glbExists(url) {
  try {
    let res = await fetch(url, { method: "HEAD", cache: "no-store" });
    if (res.ok) return true;
    res = await fetch(url, {
      method: "GET",
      headers: { Range: "bytes=0-0" },
      cache: "no-store"
    });
    return res.ok || res.status === 206;
  } catch {
    return false;
  }
}

async function bootThree(host) {
  const canvas = qs(".landing-tico__canvas", host);
  const poster = qs(".landing-tico__poster", host);
  if (!canvas) return;

  const THREE = await import("three");
  const { GLTFLoader } = await import("three/addons/loaders/GLTFLoader.js");

  const width = host.clientWidth || 320;
  const height = host.clientHeight || 360;
  const renderer = new THREE.WebGLRenderer({
    canvas,
    alpha: true,
    antialias: true,
    powerPreference: "high-performance"
  });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.75));
  renderer.setSize(width, height, false);
  renderer.outputColorSpace = THREE.SRGBColorSpace;

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(32, width / height, 0.1, 100);
  camera.position.set(0.35, 1.15, 3.2);
  camera.lookAt(0.35, 0.95, 0);

  scene.add(new THREE.HemisphereLight(0xffffff, 0xb0b8d0, 1.05));
  const key = new THREE.DirectionalLight(0xffffff, 0.85);
  key.position.set(2.5, 4, 3);
  scene.add(key);

  const occluder = new THREE.Mesh(
    new THREE.BoxGeometry(0.72, 1.45, 0.12),
    new THREE.MeshBasicMaterial({ color: 0x111827 })
  );
  occluder.position.set(0, 0.95, 0.05);
  scene.add(occluder);

  const loader = new GLTFLoader();
  const gltf = await new Promise((resolve, reject) => {
    loader.load(GLB_URL, resolve, undefined, reject);
  });

  const model = gltf.scene;
  model.position.set(-0.05, 0, -0.35);
  scene.add(model);

  const mixer = new THREE.AnimationMixer(model);
  const byName = Object.create(null);
  (gltf.animations || []).forEach((clip) => {
    byName[clip.name] = clip;
  });

  canvas.hidden = false;
  canvas.setAttribute("aria-hidden", "true");
  host.classList.add("is-tico-3d");
  host.classList.remove("is-tico-poster");
  if (poster) poster.classList.add("is-faded");

  const clock = new THREE.Clock();
  let running = true;
  let seqIndex = 0;
  const playFull = !alreadyGreeted();

  function playClip(name, loop) {
    const clip = byName[name];
    if (!clip) return null;
    const action = mixer.clipAction(clip);
    action.reset();
    action.setLoop(loop ? THREE.LoopRepeat : THREE.LoopOnce, loop ? Infinity : 1);
    action.clampWhenFinished = !loop;
    action.fadeIn(0.15).play();
    return action;
  }

  function playIdle() {
    playClip(IDLE_CLIP, true);
    if (playFull) showBubble(host);
  }

  function advanceSequence() {
    if (!playFull) {
      playIdle();
      return;
    }
    if (seqIndex >= CLIP_ORDER.length) {
      markGreeted();
      playIdle();
      return;
    }
    const name = CLIP_ORDER[seqIndex++];
    const action = playClip(name, false);
    if (!action) {
      advanceSequence();
      return;
    }
    const duration = byName[name]?.duration || 0.8;
    window.setTimeout(() => {
      action.fadeOut(0.12);
      advanceSequence();
    }, Math.max(200, duration * 1000 - 80));
  }

  function onResize() {
    const w = host.clientWidth || 320;
    const h = host.clientHeight || 360;
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    renderer.setSize(w, h, false);
  }

  window.addEventListener("resize", onResize, { passive: true });

  function frame() {
    if (!running) return;
    mixer.update(clock.getDelta());
    renderer.render(scene, camera);
    requestAnimationFrame(frame);
  }

  advanceSequence();
  requestAnimationFrame(frame);

  host._ticoDispose = () => {
    running = false;
    window.removeEventListener("resize", onResize);
    renderer.dispose();
  };
}

async function tryStart(host) {
  if (!host) return;
  if (prefersReducedMotion() || isMobileHeroLayout() || !webglAvailable()) {
    showPosterOnly(host);
    return;
  }

  const hasGlb = await glbExists(GLB_URL);
  if (!hasGlb) {
    showPosterOnly(host);
    return;
  }

  try {
    await bootThree(host);
  } catch (err) {
    console.warn("[landing-tico] WebGL path failed; using poster.", err);
    showPosterOnly(host);
  }
}

function observeAndStart(host) {
  if (!("IntersectionObserver" in window)) {
    tryStart(host);
    return;
  }
  let started = false;
  const io = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting || started) return;
        started = true;
        io.disconnect();
        tryStart(host);
      });
    },
    { threshold: 0.2, rootMargin: "0px 0px -20px 0px" }
  );
  io.observe(host);
}

export function initLandingTico(host) {
  observeAndStart(host || qs("[data-tico-host]"));
}

initLandingTico();
