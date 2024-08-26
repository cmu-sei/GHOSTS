import { ActivitySheet } from "@/components/activity-sheet";
import { SheetWrapper } from "@/components/sheet-wrapper";
import { api } from "@/generated/endpoints";
import { getSortedActivity } from "@/lib/server-utils";
import { notFound } from "next/navigation";

export default async function Page({
	params: { machineId: id },
	searchParams,
}: { params: { machineId: string }; searchParams: unknown }) {
	const sortedActivity = await getSortedActivity(
		api.getApimachinesIdactivity,
		id,
		searchParams,
		`/machines/activity/${id}?skip=0&take=50`,
	);
	const allMachines = await api.getApimachines();
	const machine = allMachines.find((machine) => machine.id === id);

	if (!machine) notFound();
	return (
		<SheetWrapper returnPath="/machines" side="right">
			<ActivitySheet
				allMachines={allMachines}
				machine={machine}
				activity={sortedActivity}
			/>
		</SheetWrapper>
	);
}
