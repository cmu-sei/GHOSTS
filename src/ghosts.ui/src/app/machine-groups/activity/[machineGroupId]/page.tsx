import { ActivitySheet } from "@/components/activity-sheet";
import { SheetWrapper } from "@/components/sheet-wrapper";
import { api } from "@/generated/endpoints";
import { getSortedActivity } from "@/lib/server-utils";

type PageProps = {
	params: { machineGroupId: string };
	searchParams?: Record<string, string | string[]>;
};

export default async function Page({ params, searchParams }: PageProps) {
	const id = Number.parseInt(params.machineGroupId);

	const sortedActivity = await getSortedActivity(
		api.getApimachinegroupsIdactivity,
		id,
		searchParams,
		`/machine-groups/activity/${params.machineGroupId}?skip=0&take=50`
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
