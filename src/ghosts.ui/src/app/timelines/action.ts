"use server";

/**
 * Server actions for timeLines CRUD
 * For server actions docs @see https://nextjs.org/docs/app/building-your-application/data-fetching/server-actions-and-mutations
 */

import { db, timeLinesTable } from "@/lib/db";
import {
	type NewTimeLine,
	type TimeLine,
	newTimeLineSchema,
	timeLineSchema,
} from "@/lib/validation";
import { eq } from "drizzle-orm";
import { revalidatePath } from "next/cache";
import { z } from "zod";

export async function createTimeLine(data: NewTimeLine) {
	const timeLine = newTimeLineSchema.parse(data);
	await db.insert(timeLinesTable).values(timeLine).run();
	revalidatePath("/timelines");
}

export async function updateTimeLine(data: TimeLine) {
	const t = timeLineSchema.parse(data);
	const { id, ...timeLine } = t;
	await db
		.update(timeLinesTable)
		.set(timeLine)
		.where(eq(timeLinesTable.id, id))
		.run();
	revalidatePath("/timelines");
}

export async function deleteTimeLine(data: unknown) {
	const id = z.number().parse(data);
	await db.delete(timeLinesTable).where(eq(timeLinesTable.id, id)).run();
	revalidatePath("/timelines");
}
