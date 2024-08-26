"use client";

import { DataTable, DataTableMoreMenu } from "@/components/data-table";
import { Badge } from "@/components/ui/badge";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { type MachineType, api } from "@/generated/endpoints";
import type { ColumnDef } from "@tanstack/react-table";
import Link from "next/link";
import { useRouter } from "next/navigation";

export function MachineTable({ machines }: { machines: MachineType[] }) {
	const machineColumns: ColumnDef<MachineType>[] = [
		{
			accessorKey: "id",
			header: "Id",
		},
		{
			accessorKey: "name",
			header: "Name",
		},
		{
			header: "Status",
			cell: ({ row: { original } }) => {
				const status = original.statusUp;

				let bg = "bg-green-500";
				switch (status) {
					case "Down":
					case "DownWithErrors":
						bg = "bg-red-500";
						break;
					case "Unknown":
					case "UpWithErrors":
						bg = "bg-orange-500";
				}
				return <Badge className={bg}>{status}</Badge>;
			},
		},
		{
			header: "More",
			cell: ({ row: { original } }) => <MachineOptions machine={original} />,
		},
	];
	return <DataTable columns={machineColumns} data={machines} />;
}

function MachineOptions({ machine }: { machine: MachineType }) {
	const router = useRouter();
	return (
		<DataTableMoreMenu jsonViewerJson={machine}>
			<DropdownMenuItem
				onClick={async () => {
					await api.deleteApimachinesId(undefined, {
						params: { id: machine.id ?? "" },
					});
					router.refresh();
				}}
			>
				Delete
			</DropdownMenuItem>
			<DropdownMenuItem>
				<Link href={`/machines/run-timeline/${machine.id}`}>Run timeLine</Link>
			</DropdownMenuItem>
			<DropdownMenuItem>
				<Link href={`/machines/activity/${machine.id}`}>View activity</Link>
			</DropdownMenuItem>
		</DataTableMoreMenu>
	);
}
