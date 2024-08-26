"use client";

import { DataTable, DataTableMoreMenu } from "@/components/data-table";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import { useApiCall } from "@/lib/utils";
import type { TimeLine } from "@/lib/validation";
import type { ColumnDef } from "@tanstack/react-table";
import Link from "next/link";
import { deleteTimeLine } from "./action";

export function TimeLineTable({ timeLines }: { timeLines: TimeLine[] }) {
	const timeLineColumns: ColumnDef<TimeLine>[] = [
		{
			accessorKey: "name",
			header: "Name",
		},
		{
			header: "TimeLine handlers",
			cell: ({ row: { original } }) => {
				return (
					<p>
						{original.timeLineHandlers
							.map((timeLineHandler) => timeLineHandler.handlerType)
							.join(" -> ")}
					</p>
				);
			},
		},
		{
			header: "Actions",
			cell: ({ row: { original } }) => (
				<TimeLineTableMore timeLine={original} />
			),
		},
	];
	return <DataTable columns={timeLineColumns} data={timeLines} />;
}

function TimeLineTableMore({ timeLine }: { timeLine: TimeLine }) {
	const call = useApiCall();
	return (
		<DataTableMoreMenu jsonViewerJson={timeLine.timeLineHandlers}>
			<DropdownMenuItem
				onClick={async () => {
					await call(deleteTimeLine(timeLine.id), "Deleted timeLine");
				}}
			>
				Delete
			</DropdownMenuItem>
			<DropdownMenuItem>
				<Link href={`/timelines/edit/${timeLine.id}`}>Edit</Link>
			</DropdownMenuItem>
		</DataTableMoreMenu>
	);
}
