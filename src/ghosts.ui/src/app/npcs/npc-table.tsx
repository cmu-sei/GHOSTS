"use client";

import { DataTable, DataTableMoreMenu } from "@/components/data-table";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { type MachineType, api, type schemas } from "@/generated/endpoints";
import type { ColumnDef } from "@tanstack/react-table";
import { useRouter } from "next/navigation";
import type { z } from "zod";

export function NpcTable({
	npcs,
	machines,
}: { npcs: NPC[]; machines: MachineType[] }) {
	const npcColumns: ColumnDef<NPC>[] = [
		{
			header: "Id",
			accessorFn: (npc) => {
				const id = npc.id;
				if (id) return id;

				return "No id";
			},
		},
		{
			header: "First name",
			accessorFn: (npc) => npc.npcProfile?.name?.first,
			filterFn: "equalsString",
		},
		{
			header: "Machine",
			accessorFn: (npc) =>
				machines.find((machine) => npc.machineId === machine.id)?.name,
		},
		{
			header: "Campaign",
			accessorKey: "campaign",
		},
		{
			header: "Enclave",
			accessorKey: "enclave",
		},
		{
			header: "Team",
			accessorKey: "team",
		},
		{
			header: "More",
			cell: ({ row: { original } }) => <NpcTableMore npc={original} />,
		},
	];

	return <DataTable columns={npcColumns} data={npcs} />;
}

export type NPC = z.infer<typeof schemas.NpcRecord>;

export function NpcTableMore({ npc }: { npc: NPC }) {
	const router = useRouter();
	return (
		<DataTableMoreMenu jsonViewerJson={npc}>
			<DropdownMenuItem
				onClick={async () => {
					const id = npc.id ?? "";
					await api.deleteApinpcsId(undefined, {
						params: { id },
					});
					router.refresh();
				}}
			>
				Delete
			</DropdownMenuItem>
		</DataTableMoreMenu>
	);
}
