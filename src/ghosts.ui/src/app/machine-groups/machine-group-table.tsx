"use client";

import { DataTable } from "@/components/data-table";
import type { GroupType, MachineType } from "@/generated/endpoints";
import type { ColumnDef } from "@tanstack/react-table";
import {
	MachineGroupAddMachines,
	MachineGroupMachines,
	MachineGroupOptions,
} from "./column-helpers";

export function MachineGroupTable({
	allMachines,
	machineGroups,
}: { allMachines: MachineType[]; machineGroups: GroupType[] }) {
	const machineGroupColumns: ColumnDef<GroupType>[] = [
		{
			accessorKey: "id",
			header: "Id",
		},
		{
			accessorKey: "name",
			header: "Name",
		},
		{
			header: "Machines",
			cell: ({ row: { original } }) => (
				<MachineGroupMachines machineGroup={original} />
			),
		},
		{
			header: "Add machines",
			cell: ({ row: { original } }) => (
				<MachineGroupAddMachines
					machineGroup={original}
					allMachines={allMachines}
				/>
			),
		},
		{
			header: "More",
			cell: ({ row: { original } }) => (
				<MachineGroupOptions machineGroup={original} />
			),
		},
	];
	return (
		<>
			<DataTable columns={machineGroupColumns} data={machineGroups} />
		</>
	);
}
