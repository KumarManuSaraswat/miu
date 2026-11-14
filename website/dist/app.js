'use strict';
const stage = document.querySelector('.mascot-stage');
const face = document.querySelector('#hero-face');
const caption = document.querySelector('#miu-caption');
const buttons = [...document.querySelectorAll('.reaction-picker button')];
const reactions = {
  happy: 'Oh, hi. I’m Miu',
  curious: 'A little curious about everything.',
  playful: 'Serious about being a little silly.',
  sleepy: 'Even little companions need a pause.'
};
let reactionTimer;
function setReaction(mood) {
  if (!Object.hasOwn(reactions, mood)) return;
  stage.dataset.mood = mood;
  face.setAttribute('href', '#face-' + mood);
  caption.textContent = reactions[mood];
  for (const button of buttons) button.setAttribute('aria-pressed', String(button.dataset.mood === mood));
  stage.classList.remove('changed');
  clearTimeout(reactionTimer);
  // Restart a short expression transition only after the visitor chooses a face.
  void stage.offsetWidth;
  stage.classList.add('changed');
  reactionTimer = setTimeout(() => stage.classList.remove('changed'), 650);
}
for (const button of buttons) button.addEventListener('click', () => setReaction(button.dataset.mood));
document.querySelector('#mascot-button').addEventListener('click', () => {
  const moods = Object.keys(reactions);
  setReaction(moods[(moods.indexOf(stage.dataset.mood) + 1) % moods.length]);
});
document.addEventListener('visibilitychange', () => {
  stage.classList.toggle('offscreen', document.hidden);
});
if ('IntersectionObserver' in window) {
  new IntersectionObserver(entries => {
    stage.classList.toggle('offscreen', !entries[0].isIntersecting || document.hidden);
  }).observe(stage);
}

// Move only the eyes, in SVG coordinates, so blinking and facial expressions
// stay independent. One frame per pointer event; no always-running render loop.
const mascots = [...document.querySelectorAll('svg:has(use[href="#miu-shell"])')];
const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
const visibleMascots = new Set(mascots);
let pointerFrame = 0;
let pointer = null;
let touchReset;
function resetGaze() {
  cancelAnimationFrame(pointerFrame);
  pointerFrame = 0;
  pointer = null;
  for (const mascot of mascots) {
    mascot.style.setProperty('--gaze-x', '0px');
    mascot.style.setProperty('--gaze-y', '0px');
  }
}
function paintGaze() {
  pointerFrame = 0;
  if (!pointer || document.hidden || reducedMotion.matches) return;
  for (const mascot of visibleMascots) {
    const matrix = mascot.getScreenCTM();
    if (!matrix) continue;
    const local = new DOMPoint(pointer.x, pointer.y).matrixTransform(matrix.inverse());
    const dx = (local.x - 175) / 140;
    const dy = (local.y - 174) / 140;
    const distance = Math.hypot(dx, dy);
    const limit = Math.max(1, distance);
    mascot.style.setProperty('--gaze-x', (dx / limit * 11).toFixed(2) + 'px');
    mascot.style.setProperty('--gaze-y', (dy / limit * 8).toFixed(2) + 'px');
  }
}
function followPointer(event) {
  if (reducedMotion.matches || document.hidden) return;
  pointer = {x: event.clientX, y: event.clientY};
  if (!pointerFrame) pointerFrame = requestAnimationFrame(paintGaze);
}
document.addEventListener('pointermove', event => {
  if (event.pointerType !== 'touch') followPointer(event);
}, {passive: true});
document.addEventListener('pointerdown', event => {
  if (event.pointerType !== 'touch' || !event.target.closest('#mascot-button, svg:has(use[href="#miu-shell"])')) return;
  followPointer(event);
  clearTimeout(touchReset);
  touchReset = setTimeout(resetGaze, 1400);
}, {passive: true});
document.documentElement.addEventListener('pointerleave', resetGaze);
window.addEventListener('blur', resetGaze);
window.addEventListener('scroll', resetGaze, {passive: true});
document.addEventListener('visibilitychange', resetGaze);
reducedMotion.addEventListener('change', resetGaze);
if ('IntersectionObserver' in window) {
  const observer = new IntersectionObserver(entries => {
    for (const entry of entries) {
      if (entry.isIntersecting) visibleMascots.add(entry.target);
      else {
        visibleMascots.delete(entry.target);
        entry.target.style.setProperty('--gaze-x', '0px');
        entry.target.style.setProperty('--gaze-y', '0px');
      }
    }
  });
  for (const mascot of mascots) observer.observe(mascot);
}
