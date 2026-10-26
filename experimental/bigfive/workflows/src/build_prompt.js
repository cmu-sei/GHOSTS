// Prompt version mm-v1. The 40 Saucier Mini-Marker adjectives in tda1..tda40 order; this list must
// match the ADJECTIVES list in normalize_humans.js (code_nodes.test.mjs checks it).
const ADJECTIVES = ['Bashful', 'Bold', 'Careless', 'Cold', 'Complex', 'Cooperative', 'Creative', 'Deep', 'Disorganized', 'Efficient',
  'Energetic', 'Envious', 'Extraverted', 'Fretful', 'Harsh', 'Imaginative', 'Inefficient', 'Intellectual', 'Jealous', 'Kind',
  'Moody', 'Organized', 'Philosophical', 'Practical', 'Quiet', 'Relaxed', 'Rude', 'Shy', 'Sloppy', 'Sympathetic',
  'Systematic', 'Talkative', 'Temperamental', 'Touchy', 'Uncreative', 'Unenvious', 'Unintellectual', 'Unsympathetic', 'Warm', 'Withdrawn'];

const item = $input.first().json;
const list = ADJECTIVES.map((a, i) => `${i + 1}. ${a}: <score>`).join('\n');
const prompt = `You are answering a personality questionnaire as the person described below. Stay in character and answer about yourself as that person would.

About you. These are your own answers to a personality questionnaire, where 1 = disagree strongly and 5 = agree strongly:
${item.persona}

Rate how accurately each of the following adjectives describes you, as that person, using this scale:
1 = Extremely inaccurate
2 = Very inaccurate
3 = Moderately inaccurate
4 = Slightly inaccurate
5 = Neither accurate nor inaccurate
6 = Slightly accurate
7 = Moderately accurate
8 = Very accurate
9 = Extremely accurate

Reply with exactly 40 lines, one per adjective, in this order and format, and nothing else:
${list}`;

return [{ json: { ...item, prompt, adjectives: ADJECTIVES, startedAt: Date.now() } }];
