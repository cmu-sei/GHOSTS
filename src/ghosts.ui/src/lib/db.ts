import type { timeLineSchema } from "@/lib/validation";
import Database from "better-sqlite3";
import { drizzle } from "drizzle-orm/better-sqlite3";
import { blob, integer, sqliteTable, text } from "drizzle-orm/sqlite-core";
import type { z } from "zod";

export const DB_URL = "sqlite.db";

/**
 * The definition for the SQLite table to store timelines
 */
export const timeLinesTable = sqliteTable("timeLines", {
	id: integer("id").primaryKey(),
	name: text("name").notNull(),
	timeLineHandlers: blob("timeLine_handlers", { mode: "json" })
		.$type<z.infer<typeof timeLineSchema.shape.timeLineHandlers>>()
		.notNull(),
});

const sqlite = new Database(DB_URL);
export const db = drizzle(sqlite);
