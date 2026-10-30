import { AssetModelIcon } from '../../asset/asset-model/asset-model.icons';

export type SkillStatus = 'Active' | 'Inactive';
export type SkillType = 'Hard Skill' | 'Soft Skill';

export interface SkillRecord {
  code: string;
  name: string;
  category: string;
  type: SkillType;
  description: string;
  icon: AssetModelIcon;
  status: SkillStatus;
  // Proficiency levels from lowest to highest; empty when the skill isn't graded
  levels: string[];
  // Designations that normally need this skill
  relatedRoles: string[];
  certificationRequired: boolean;
  certificationValidityMonths: number;
  assessmentRequired: boolean;
  createdOn: string;
  updatedOn: string;
}

export interface SkillFilters {
  search: string;
  category: string;
  type: '' | SkillType;
}
