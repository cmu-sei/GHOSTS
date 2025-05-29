// drizzle.config.ts
import { defineConfig } from "drizzle-kit";

export const DB_URL = "sqlite.db";

export default defineConfig({
  schema: "./src/lib/db.ts",
  dialect: "sqlite",
  dbCredentials: {
    url: DB_URL,
  },
});
