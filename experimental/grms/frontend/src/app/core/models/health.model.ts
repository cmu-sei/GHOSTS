export interface HealthStatus {
  status: string;
  llm_backend: string;
  llm_model: string;
  leaders_loaded: number;
  populations_loaded: number;
}
