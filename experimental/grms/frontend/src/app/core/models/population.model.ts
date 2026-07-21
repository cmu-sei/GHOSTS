export interface SegmentDemographics {
  urbanization: number;
  education_level: number;
  median_age: number;
  economic_class: string;
}

export interface SegmentDisposition {
  government_trust: number;
  nationalism: number;
  media_exposure: number;
  social_media_activity: number;
  protest_propensity: number;
  compliance_baseline: number;
  information_sources: string[];
}

export interface SegmentResponseParams {
  rally_around_flag_coefficient: number;
  economic_sensitivity: number;
  fatigue_rate: number;
  amplification_factor: number;
}

export interface PopulationSegment {
  name: string;
  percentage: number;
  demographics: SegmentDemographics;
  disposition: SegmentDisposition;
  response_params: SegmentResponseParams;
}

export interface PopulationProfile {
  country: string;
  total_population: number;
  period: string;
  segments: PopulationSegment[];
}
