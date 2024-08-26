import { ActivitySheet } from "@/components/activity-sheet";
import { SheetWrapper } from "@/components/sheet-wrapper";
import { api } from "@/generated/endpoints";
import { getSortedActivity } from "@/lib/server-utils";

export default async function Page({
	params: { machineGroupId },
	searchParams,
}: { params: { machineGroupId: string }; searchParams: unknown }) {
	const id = Number.parseInt(machineGroupId);
	const sortedActivity = await getSortedActivity(
		api.getApimachinegroupsIdactivity,
		id,
		searchParams,
		`/machine-groups/activity/${machineGroupId}?skip=0&take=50`,
	);

	const machineGroup = await api.getApimachinegroupsId({ params: { id } });
	const allMachines = await api.getApimachines();
	return (
		<SheetWrapper returnPath="/machine-groups" side="right">
			<ActivitySheet
				allMachines={allMachines}
				machineGroup={machineGroup}
				activity={sortedActivity}
			/>
		</SheetWrapper>
	);
}
