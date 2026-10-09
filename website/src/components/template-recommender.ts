export type TemplateAnswers = {
  impact: 'low' | 'medium' | 'high';
  alternatives: boolean;
  detailedRisks: boolean;
  standard: 'none' | 'canonical' | 'madr';
  formality: boolean;
};

export type TemplateRecommendation = 'minimal' | 'extended' | 'madr-4';

export function recommendTemplate(answers: TemplateAnswers): TemplateRecommendation {
  if (answers.standard === 'madr') return 'madr-4';
  const detailScore = Number(answers.alternatives) + Number(answers.detailedRisks) + Number(answers.formality);
  if (answers.impact === 'high' || detailScore >= 2) return 'extended';
  return 'minimal';
}
