export interface Npc {
  id?: string;
  npcProfile?: NpcProfile;
  campaign?: string;
  enclave?: string;
  team?: string;
  machineProfile?: MachineProfile;
  createdUtc?: string;
}

export interface NpcProfile {
  id?: string;
  address?: Address;
  birthdate?: string;
  email?: string;
  family?: Family;
  name?: Name;
  username?: string;
  randomization?: number;
  accessProfile?: AccessProfile;
  accountActivity?: AccountActivity;
  attributes?: Attributes;
  bloodType?: string;
  careerProfile?: CareerProfile;
  criminalViolentConductHistory?: CriminalViolentConductHistory;
  educationProfile?: EducationProfile;
  financialConsiderations?: FinancialConsiderations;
  foreignActivityBusinessTravel?: ForeignActivityBusinessTravel;
  identificationNumbers?: IdentificationNumbers;
  mentalHealth?: MentalHealth;
  photos?: Photos;
  physicalCharacteristics?: PhysicalCharacteristics;
  relationshipsAssociations?: RelationshipsAssociations;
  skillsKSAs?: string[];
}

export interface Address {
  streetAddress?: string;
  city?: string;
  state?: string;
  postalCode?: string;
  country?: string;
}

export interface Family {
  spouse?: Person;
  mother?: Person;
  father?: Person;
  siblings?: Person[];
  children?: Person[];
  exSpouse?: Person[];
}

export interface Person {
  name?: Name;
  birthdate?: string;
  relationship?: string;
}

export interface Name {
  prefix?: string;
  first?: string;
  middle?: string;
  last?: string;
  suffix?: string;
}

export interface AccessProfile {
  clearance?: string;
  access?: string[];
  explosiveAccess?: string;
  cBRNAccess?: string;
}

export interface AccountActivity {
  lastLogon?: string;
  activity?: string;
  favoriteWebsites?: string[];
  favoriteColor?: string;
  favoriteFood?: string;
  favoriteBook?: string;
}

export interface Attributes {
  agreeableness?: number;
  conscientiousness?: number;
  creativity?: number;
  extroversion?: number;
  neuroticism?: number;
  openness?: number;
}

export interface CareerProfile {
  currentJob?: Job;
  previousJobs?: Job[];
  workEthic?: number;
  teamValue?: number;
  strengths?: string[];
  weaknesses?: string[];
}

export interface Job {
  title?: string;
  company?: string;
  startDate?: string;
  endDate?: string;
}

export interface CriminalViolentConductHistory {
  hasCriminalHistory?: boolean;
  hasViolentHistory?: boolean;
  crimes?: Crime[];
}

export interface Crime {
  type?: string;
  description?: string;
  date?: string;
}

export interface EducationProfile {
  highSchool?: Education;
  college?: Education;
  graduate?: Education;
}

export interface Education {
  name?: string;
  degree?: string;
  major?: string;
  graduationDate?: string;
  gPA?: number;
}

export interface FinancialConsiderations {
  income?: number;
  debt?: number;
  creditScore?: number;
  hasFinancialIssues?: boolean;
}

export interface ForeignActivityBusinessTravel {
  hasTravel?: boolean;
  countries?: string[];
}

export interface IdentificationNumbers {
  ssN?: string;
  driversLicense?: string;
  passport?: string;
}

export interface MentalHealth {
  hasMentalHealthIssues?: boolean;
  conditions?: string[];
  insecurity?: number;
  depression?: number;
  anxiety?: number;
  anger?: number;
}

export interface Photos {
  photo?: string;
  photoType?: string;
}

export interface PhysicalCharacteristics {
  height?: string;
  weight?: string;
  hairColor?: string;
  eyeColor?: string;
  race?: string;
  sex?: string;
}

export interface RelationshipsAssociations {
  friends?: Person[];
  enemies?: Person[];
  colleagues?: Person[];
}

export interface CampaignInformation {
  campaign?: string;
  description?: string;
}

export interface MachineProfile {
  username?: string;
  password?: string;
  machineId?: string;
}

export interface CreateNpcRequest {
  npcProfile: NpcProfile;
  campaign?: string;
  enclave?: string;
  team?: string;
  machineProfile?: MachineProfile;
}

export interface GenerateNpcRequest {
  campaign: string;
  enclave: string;
  team: string;
  number: number;
}
