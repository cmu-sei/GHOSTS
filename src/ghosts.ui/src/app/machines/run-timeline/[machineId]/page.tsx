import { SheetWrapper } from "@/components/sheet-wrapper";
import { TimeLineRunner } from "@/components/timeline-runner";
import { api } from "@/generated/endpoints";
import { db, timeLinesTable } from "@/lib/db";

export default async function Page({
	params: { machineId },
}: { params: { machineId: string } }) {
	const machine = await api.getApimachinesId({ params: { id: machineId } });
	const timeLines = await db.select().from(timeLinesTable);
	return (
		<SheetWrapper returnPath="/machines" side="right">
			<TimeLineRunner
				machine={machine}
				timeLines={timeLines}
				returnPath="/machines"
			/>
		</SheetWrapper>
	);
}
