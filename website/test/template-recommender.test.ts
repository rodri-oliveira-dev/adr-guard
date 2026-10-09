import { describe, expect, it } from 'vitest';
import { recommendTemplate } from '../src/components/template-recommender';

describe('recommendTemplate', () => {
  it('prefers MADR when the team standard requires it', () => {
    expect(recommendTemplate({ impact: 'low', alternatives: false, detailedRisks: false, standard: 'madr', formality: false })).toBe('madr-4');
  });

  it('uses extended for high-impact decisions', () => {
    expect(recommendTemplate({ impact: 'high', alternatives: false, detailedRisks: false, standard: 'canonical', formality: false })).toBe('extended');
  });

  it('uses minimal for focused low-impact decisions', () => {
    expect(recommendTemplate({ impact: 'low', alternatives: false, detailedRisks: false, standard: 'none', formality: false })).toBe('minimal');
  });
});
