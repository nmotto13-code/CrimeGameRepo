/**
 * One-off generator for the Crime City: Detective title screen background.
 * Saves directly to Assets/Sprites/Backgrounds/title_screen.png
 *
 * Usage:  node generate-title.js
 */

import { GoogleGenAI } from '@google/genai';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import 'dotenv/config';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const OUT_PATH = path.resolve(
  __dirname, '../../Assets/Sprites/Backgrounds/title_screen.png'
);

const PROMPT = `
A lone detective in a long trench coat and fedora stands at the rain-slicked
edge of a city rooftop at night, back completely to camera, silhouetted against
a vast noir cityscape stretching to the horizon below. Rain falls in fine silver
threads catching ambient light. Wet streets far below reflect amber streetlamps
and faint neon signs. Fog drifts between dark towers. The sky above is a deep
storm-bruised blue-black with heavy storm clouds — the upper third of the frame
is open dark sky. Cinematic composition. Shallow depth of field, city softly out
of focus. Photorealistic, film noir aesthetic, heavy film grain, desaturated
colour palette with warm amber and cold steel blue as the only accent colours.
No text, no logos, no UI elements. Portrait 9:16 aspect ratio.
`.replace(/\n/g, ' ').trim();

const IMAGEN_MODEL = 'imagen-4.0-generate-001';
const GEMINI_MODEL  = 'gemini-2.5-flash-image';

if (!process.env.GOOGLE_API_KEY) {
  console.error('ERROR: GOOGLE_API_KEY not set in .env');
  process.exit(1);
}

const ai = new GoogleGenAI({ apiKey: process.env.GOOGLE_API_KEY });

async function generate() {
  console.log('\n  Crime City: Detective — title screen');
  console.log(`  Output: ${OUT_PATH}\n`);

  let bytes;

  try {
    console.log('  Trying Google Imagen 4 ...');
    const res = await ai.models.generateImages({
      model: IMAGEN_MODEL,
      prompt: PROMPT,
      config: { numberOfImages: 1, outputMimeType: 'image/png' },
    });
    bytes = res.generatedImages?.[0]?.image?.imageBytes;
    if (!bytes) throw new Error('No image bytes returned');
    console.log('  Imagen 4 succeeded.');
  } catch (err) {
    const msg = err?.message ?? String(err);
    const unavailable = msg.includes('NOT_FOUND') || msg.includes('not found')
      || msg.includes('paid') || msg.includes('upgrade')
      || msg.includes('INVALID_ARGUMENT');
    if (!unavailable) throw err;

    console.log('  Imagen unavailable — falling back to Gemini Flash...');
    const res = await ai.models.generateContent({
      model: GEMINI_MODEL,
      contents: [{ role: 'user', parts: [{ text: PROMPT }] }],
      config: { responseModalities: ['IMAGE', 'TEXT'] },
    });
    const parts = res.candidates?.[0]?.content?.parts ?? [];
    const imgPart = parts.find(p => p.inlineData?.mimeType?.startsWith('image/'));
    if (!imgPart) throw new Error('No image returned from Gemini Flash');
    bytes = imgPart.inlineData.data;
    console.log('  Gemini Flash succeeded.');
  }

  fs.mkdirSync(path.dirname(OUT_PATH), { recursive: true });
  fs.writeFileSync(OUT_PATH, Buffer.from(bytes, 'base64'));
  console.log(`\n  ✓ Saved: ${path.basename(OUT_PATH)}`);
  console.log('  Location: Assets/Sprites/Backgrounds/title_screen.png\n');
}

generate().catch(err => {
  console.error('\n  FAILED:', err.message ?? err);
  process.exit(1);
});
