import type { Variants } from 'motion/react';

const transition = { duration: .18, ease: [.2, .8, .2, 1] as const };
export const canvasMotion: Variants = { hidden: { opacity: 0, y: 6 }, visible: { opacity: 1, y: 0, transition }, exit: { opacity: 0, transition: { duration: .1 } } };
export const panelMotion: Variants = { closed: { opacity: 0, y: 8, scale: .98 }, open: { opacity: 1, y: 0, scale: 1, transition } };
const still = { opacity: 1, y: 0, scale: 1, transition: { duration: 0 } };
export const stillCanvasMotion: Variants = { hidden: still, visible: still, exit: still };
export const stillPanelMotion: Variants = { closed: still, open: still };
