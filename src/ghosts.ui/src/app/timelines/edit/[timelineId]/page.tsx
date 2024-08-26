import { SheetWrapper } from "@/components/sheet-wrapper";
import { db, timeLinesTable } from "@/lib/db";
import { eq } from "drizzle-orm";
import { notFound } from "next/navigation";
import { TimeLineForm } from "../../timeline-form";

export default async function Page({
	params: { timelineId },
}: { params: { timelineId: string } }) {
	const id = Number.parseInt(timelineId);
	const timeLine = await db
		.select()
		.from(timeLinesTable)
		.where(eq(timeLinesTable.id, id))
		.get();

	if (!timeLine) notFound();

	return (
		<SheetWrapper returnPath="/timelines" side="right">
			<TimeLineForm timeLine={timeLine} />
		</SheetWrapper>
	);
}
