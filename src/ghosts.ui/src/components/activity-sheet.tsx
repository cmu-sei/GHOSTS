"use client";

import { SheetTitle } from "@/components/ui/sheet";
import type { MachineActivityType, MachineType } from "@/generated/endpoints";
import type { ColumnDef } from "@tanstack/react-table";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { DataTable } from "./data-table";
import type { MachineOrMachineGroup } from "./timeline-runner";
import { Input } from "./ui/input";

/**
 * A component to display activity of a machine or machine group in a table
 */
export function ActivitySheet({
	activity,
	machine,
	machineGroup,
	allMachines,
}: {
	activity: MachineActivityType[];
	allMachines: MachineType[];
} & MachineOrMachineGroup) {
	const name = machine ? machine.name : machineGroup.name;
	const entityName = `${machine ? "machine" : "machine group"} '${name}'`;

	const activityColumns: ColumnDef<MachineActivityType>[] = [
		{
			header: "Handler type",
			accessorKey: "handler",
		},
		{
			header: "Command",
			accessorKey: "command",
		},
		{
			header: "Command arg",
			accessorKey: "commandArg",
		},
		{
			header: "Created UTC",
			accessorKey: "createdUtc",
		},
	];

	// Add the machine link column if we are viewing the activity of a machine group
	if (machineGroup) {
		activityColumns.push({
			header: "Machine",
			cell: ({ row: { original } }) => {
				const machine = allMachines.find(
					(machine) => machine.id === original.machineId,
				);
				if (machine) {
					return (
						<Link
							href={`/machines/activity/${original.machineId}`}
							className="underline"
						>
							{machine.name ?? "No machine name"}
						</Link>
					);
				}
				return "Machine not found";
			},
		});
	}

	return (
		<>
			<SheetTitle>{`Activity for ${entityName}`}</SheetTitle>
			<ActivityPaginator />
			<DataTable columns={activityColumns} data={activity} />
		</>
	);
}

import {
	Pagination,
	PaginationContent,
	PaginationItem,
	PaginationNext,
	PaginationPrevious,
} from "@/components/ui/pagination";
import { useEffect, useState } from "react";

function ActivityPaginator() {
	const router = useRouter();
	const path = usePathname();
	const searchParams = useSearchParams();

	const currentTake = Number.parseInt(
		searchParams.get("take")?.toString() ?? "50",
	);
	const currentSkip = Number.parseInt(
		searchParams.get("skip")?.toString() ?? "0",
	);

	const [take, setTake] = useState(currentTake);

	useEffect(() => {
		const timeoutId = setTimeout(() => {
			if (!Number.isNaN(take)) router.push(`${path}?skip=0&take=${take}`);
		}, 1000);
		return () => clearTimeout(timeoutId);
	}, [take, path, router.push]);

	const [page, setPage] = useState(1);

	useEffect(() => {
		const timeoutId = setTimeout(() => {
			if (!Number.isNaN(take))
				router.push(`${path}?skip=${(page - 1) * take}&take=${take}`);
		}, 1000);
		return () => clearTimeout(timeoutId);
	}, [page, take, path, router.push]);

	const prevTake = currentSkip - currentTake;
	const prevHref = `${path}?skip=${
		prevTake < 0 ? 0 : prevTake
	}&take=${currentTake}`;

	const nextHref = `${path}?skip=${
		currentSkip + currentTake
	}&take=${currentTake}`;

	return (
		<Pagination>
			<PaginationContent>
				<PaginationItem>
					<PaginationPrevious href={prevHref} />
				</PaginationItem>
				<PaginationItem>
					<Input
						className="w-32 h-8"
						value={page}
						onChange={(e) => setPage(Number.parseInt(e.target.value))}
						type="number"
						min={1}
					/>
				</PaginationItem>
				<PaginationItem>
					<PaginationNext href={nextHref} />
				</PaginationItem>
				<PaginationItem>
					<Input
						className="w-32 h-8"
						value={take}
						onChange={(e) => setTake(Number.parseInt(e.target.value))}
						type="number"
						min={1}
					/>
				</PaginationItem>
			</PaginationContent>
		</Pagination>
	);
}
