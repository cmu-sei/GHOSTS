"use client";

import { DataTableMoreMenu } from "@/components/data-table";
import { Button } from "@/components/ui/button";
import {
	DropdownMenu,
	DropdownMenuContent,
	DropdownMenuItem,
	DropdownMenuLabel,
	DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { type GroupType, type MachineType, api } from "@/generated/endpoints";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { PropsWithChildren } from "react";

function MachinesDrowpdown({
	children,
	triggerText,
	dropdownDesc,
}: {
	triggerText: string;
	dropdownDesc: string;
} & PropsWithChildren) {
	return (
		<DropdownMenu>
			<DropdownMenuTrigger asChild>
				<Button>{triggerText}</Button>
			</DropdownMenuTrigger>
			<DropdownMenuContent className="p-4">
				<DropdownMenuLabel>{dropdownDesc}</DropdownMenuLabel>
				{children}
			</DropdownMenuContent>
		</DropdownMenu>
	);
}

export function MachineGroupMachines({
	machineGroup,
}: {
	machineGroup: GroupType;
}) {
	const machines = machineGroup.machines ?? [];
	const router = useRouter();

	return (
		<MachinesDrowpdown
			triggerText={`View machines (${machines.length})`}
			dropdownDesc="Machines in this group, click to remove"
		>
			{machines.map((machine) => {
				return (
					<DropdownMenuItem
						key={machine.id}
						onClick={async () => {
							const groupMachinesWithoutDeleted = machines
								.filter((m) => m.id !== machine.id)
								.map((machine2) => ({
									groupId: machineGroup.id,
									machineId: machine2.id,
								}));
							await api.putApimachinegroupsId(
								{
									id: machineGroup.id,
									name: machineGroup.name,
									groupMachines: groupMachinesWithoutDeleted,
								},
								{ params: { id: machineGroup.id?.toString() ?? "" } },
							);
							router.refresh();
						}}
					>
						{machine.name}
					</DropdownMenuItem>
				);
			})}
		</MachinesDrowpdown>
	);
}

export function MachineGroupAddMachines({
	machineGroup,
	allMachines,
}: { machineGroup: GroupType; allMachines: MachineType[] }) {
	const router = useRouter();

	const machinesNotInMachineGroup = allMachines.filter(
		(machine) =>
			!machineGroup.machines?.find((machine2) => machine2.id === machine.id),
	);
	const currentGroupMachines = (machineGroup.machines ?? []).map((machine) => ({
		groupId: machineGroup.id,
		machineId: machine.id,
	}));

	return (
		<MachinesDrowpdown
			triggerText={`Add machines (${machinesNotInMachineGroup.length})`}
			dropdownDesc="Machines available to add, click to add"
		>
			{machinesNotInMachineGroup.map((machine) => (
				<DropdownMenuItem
					key={machine.id}
					onClick={async () => {
						await api.putApimachinegroupsId(
							{
								id: machineGroup.id,
								name: machineGroup.name,
								groupMachines: [
									...currentGroupMachines,
									{ groupId: machineGroup.id, machineId: machine.id },
								],
							},
							{
								params: { id: machineGroup.id?.toString() ?? "" },
							},
						);
						router.refresh();
					}}
				>
					{machine.name}
				</DropdownMenuItem>
			))}
		</MachinesDrowpdown>
	);
}

export function MachineGroupOptions({
	machineGroup,
}: { machineGroup: GroupType }) {
	const router = useRouter();
	return (
		<DataTableMoreMenu jsonViewerJson={machineGroup}>
			<DropdownMenuItem
				onClick={async () => {
					await api.deleteApimachinegroupsId(undefined, {
						params: { id: machineGroup.id ?? 0 },
					});
					router.refresh();
				}}
			>
				Delete
			</DropdownMenuItem>
			<DropdownMenuItem>
				<Link href={`/machine-groups/run-timeline/${machineGroup.id}`}>
					Run timeLine
				</Link>
			</DropdownMenuItem>
			<DropdownMenuItem>
				<Link href={`/machine-groups/activity/${machineGroup.id}`}>
					View activity
				</Link>
			</DropdownMenuItem>
		</DataTableMoreMenu>
	);
}
