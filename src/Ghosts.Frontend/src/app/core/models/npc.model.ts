export interface Npc {
  id?: string;
  npcProfile?: NpcProfile;
  scenarioId?: number;
  campaign?: string;
  enclave?: string;
  team?: string;
  machineProfile?: MachineProfile;
  createdUtc?: string;
}

export interface NpcProfile {
  id?: string;
  name?: Name;
  address?: Address[];
  email?: string;
  password?: string;
  homePhone?: string;
  cellPhone?: string;
  preferences?: Preference[];
  unit?: MilitaryUnit;
  rank?: Rank;
  education?: EducationProfile;
  employment?: EmploymentProfile;
  biologicalSex?: string;
  birthdate?: string;
  health?: HealthProfile;
  attributes?: { [key: string]: string };
  relationships?: RelationshipProfile[];
  family?: FamilyProfile;
  finances?: FinancialProfile;
  mentalHealth?: MentalHealthProfile;
  foreignTravel?: ForeignTravelProfile;
  career?: CareerProfile;
  workstation?: MachineProfile;
  insiderThreat?: InsiderThreatProfile;
  accounts?: Account[];
  motivationalProfile?: MotivationalProfile;
  cac?: string;
  photoLink?: string;
  created?: string;
}

export interface Name {
  prefix?: string;
  first?: string;
  middle?: string;
  last?: string;
  suffix?: string;
}

export interface Address {
  addressType?: string;
  name?: string;
  address1?: string;
  address2?: string;
  city?: string;
  state?: string;
  postalCode?: string;
}

export interface Preference {
  id?: number;
  score?: number;
  name?: string;
  meta?: string;
}

export interface MilitaryUnit {
  country?: string;
  address?: Address;
  sub?: Unit[];
}

export interface Unit {
  name?: string;
  type?: string;
  nick?: string;
  hq?: string;
  sub?: Unit[];
}

export interface Rank {
  branch?: string;
  classification?: string;
  name?: string;
  abbr?: string;
  pay?: string;
  billet?: string;
  mos?: string;
  mosid?: string;
}

export interface HealthProfile {
  height?: number; // inches
  weight?: number; // lbs
  bloodType?: string;
  preferredMeal?: string;
  medicalConditions?: MedicalCondition[];
}

export interface MedicalCondition {
  name?: string;
  prescriptions?: Prescription[];
}

export interface Prescription {
  name?: string;
}

export interface EducationProfile {
  degrees?: Degree[];
}

export interface Degree {
  level?: string;
  degreeType?: string;
  major?: string;
  school?: School;
}

export interface School {
  name?: string;
  location?: string;
}

export interface EmploymentProfile {
  employmentRecords?: EmploymentRecord[];
}

export interface EmploymentRecord {
  company?: string;
  startDate?: string;
  endDate?: string;
  department?: string;
  organization?: string;
  jobTitle?: string;
  level?: number;
  salary?: number;
  manager?: string;
  emailSuffix?: string;
  email?: string;
  address?: Address;
  phone?: string;
  employmentStatus?: string;
}

export interface FamilyProfile {
  members?: FamilyMember[];
}

export interface FamilyMember {
  name?: Name;
  relationship?: string;
}

export interface FinancialProfile {
  netWorth?: number;
  totalDebt?: number;
  creditCards?: CreditCard[];
}

export interface CreditCard {
  number?: string;
  type?: string;
}

export interface MentalHealthProfile {
  interpersonalSkills?: number;
  adherenceToPolicy?: number;
  enthusiasmAndAttitude?: number;
  openToFeedback?: number;
  generalPerformance?: number;
  overallPerformance?: number;
  iq?: number;
  spideySense?: number;
  senseSomethingIsWrongQuotient?: number;
  happyQuotient?: number;
  melancholyQuotient?: number;
}

export interface ForeignTravelProfile {
  trips?: Trip[];
}

export interface Trip {
  code?: string;
  country?: string;
  destination?: string;
  arriveDestination?: string;
  departDestination?: string;
}

export interface RelationshipProfile {
  id?: number;
  with?: string;
  type?: string;
}

export interface CareerProfile {
  workEthic?: number;
  teamValue?: number;
  strengths?: NamedTrait[];
  weaknesses?: NamedTrait[];
}

export interface NamedTrait {
  name?: string;
}

export interface InsiderThreatProfile {
  access?: AccessProfile;
  criminalViolentOrAbusiveConduct?: InsiderThreatCategory;
  financialConsiderations?: InsiderThreatCategory;
  foreignConsiderations?: InsiderThreatCategory;
  judgementCharacterAndPsychologicalConditions?: InsiderThreatCategory;
  professionalLifecycleAndPerformance?: InsiderThreatCategory;
  securityAndComplianceIncidents?: InsiderThreatCategory;
  substanceAbuseAndAddictiveBehaviors?: InsiderThreatCategory;
  technicalActivity?: InsiderThreatCategory;
  isBackgroundCheckStatusClear?: boolean;
}

export interface InsiderThreatCategory {
  id?: number;
  relatedEvents?: InsiderThreatEvent[];
}

export interface AccessProfile extends InsiderThreatCategory {
  explosivesAccess?: string;
  systemsAccess?: string;
  cbrnAccess?: string;
  physicalAccess?: string;
  securityClearance?: string;
  isDoDSystemsPrivilegedUser?: boolean;
}

export interface InsiderThreatEvent {
  id?: number;
  description?: string;
  correctiveAction?: string;
  reportedBy?: string;
  reported?: string;
}

export interface Account {
  id?: number;
  username?: string;
  password?: string;
  url?: string;
}

export interface MotivationalProfile {
  acceptance?: number;
  beauty?: number;
  curiosity?: number;
  eating?: number;
  family?: number;
  honor?: number;
  idealism?: number;
  independence?: number;
  order?: number;
  physicalActivity?: number;
  power?: number;
  saving?: number;
  socialContact?: number;
  status?: number;
  tranquility?: number;
  vengeance?: number;
}

export interface MachineProfile {
  name?: string;
  domain?: string;
  username?: string;
  password?: string;
  ipAddress?: string;
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
  scenarioId?: number;
}
