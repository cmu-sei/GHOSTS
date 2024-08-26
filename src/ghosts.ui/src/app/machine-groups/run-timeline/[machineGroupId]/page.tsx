import { SheetWrapper } from "@/components/sheet-wrapper";
import { TimeLineRunner } from "@/components/timeline-runner";
import { api } from "@/generated/endpoints";
import { db, timeLinesTable } from "@/lib/db";

export default async function Page({
	params: { machineGroupId },
}: { params: { machineGroupId: string } }) {
	const machineGroup = await api.getApimachinegroupsId({
		params: { id: Number.parseInt(machineGroupId) },
	});
	const timeLines = await db.select().from(timeLinesTable);

	return (
		<SheetWrapper returnPath="/machine-groups" side="right">
			<TimeLineRunner
				machineGroup={machineGroup}
				timeLines={timeLines}
				returnPath="/machine-groups"
			/>
		</SheetWrapper>
	);
}
