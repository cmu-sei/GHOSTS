import { DB_URL } from "@/lib/db";
import { defineConfig } from "drizzle-kit";

export default defineConfig({
	schema: "./src/lib/db.ts",
	driver: "better-sqlite",
	dbCredentials: {
		url: DB_URL,
	},
});
