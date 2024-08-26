"use client";

import {
	type Column,
	type ColumnDef,
	flexRender,
	getCoreRowModel,
	getFilteredRowModel,
	useReactTable,
} from "@tanstack/react-table";

import {
	Table,
	TableBody,
	TableCell,
	TableHead,
	TableHeader,
	TableRow,
} from "@/components/ui/table";
import { MoreHorizontal } from "lucide-react";
import { type PropsWithChildren, useEffect, useState } from "react";
import { JSONViewer } from "./json-utils";
import { Button } from "./ui/button";
import {
	DropdownMenu,
	DropdownMenuContent,
	DropdownMenuLabel,
	DropdownMenuTrigger,
} from "./ui/dropdown-menu";
import { Input } from "./ui/input";

interface DataTableProps<TData, TValue> {
	columns: ColumnDef<TData, TValue>[];
	data: TData[];
}

/**
 * A dropdown menu to perform actions on a table row
 * Includes a json viewer to view the provided (usually the row) JSON
 */
export function DataTableMoreMenu<T>({
	children,
	jsonViewerJson,
}: { jsonViewerJson: T } & PropsWithChildren) {
	return (
		<DropdownMenu>
			<DropdownMenuTrigger asChild>
				<Button className="h-8 w-8 p-0">
					<MoreHorizontal className="h-4 w-4" />
				</Button>
			</DropdownMenuTrigger>
			<DropdownMenuContent className="flex flex-col gap-2 p-2 items-start">
				<DropdownMenuLabel>Actions</DropdownMenuLabel>
				{children}
				<JSONViewer json={jsonViewerJson} />
			</DropdownMenuContent>
		</DropdownMenu>
	);
}

/**
 * Default ShadCN UI DataTable component
 * Added a simple implementation of selecting columns to filter
 * Rows are filtered by a string search of the value of the selected column
 */
export function DataTable<TData, TValue>({
	columns,
	data,
}: DataTableProps<TData, TValue>) {
	// Add a rawJson column for searching the raw json
	columns = [
		...columns,
		{
			id: "rawJson",
			accessorFn: (row) => JSON.stringify(row),
		} as ColumnDef<TData, TValue>,
	];

	// @ts-expect-error ts doesn't like this
	columns = columns.map(
		// @ts-expect-error same
		(col) => ({ ...col, filterFn: "test" }) satisfies ColumnDef<TData, TValue>,
	);

	const table = useReactTable({
		data,
		columns,
		state: {
			columnVisibility: {
				// Hide the rawJson column
				rawJson: false,
			},
		},
		columnResizeMode: "onChange",
		getCoreRowModel: getCoreRowModel(),
		getFilteredRowModel: getFilteredRowModel(),

		/**
		 * Custom search fn because default tanstack table filter fn doesn't work with nullable cols
		 * @see https://github.com/TanStack/table/issues/4919
		 */
		filterFns: {
			test: (row, filterColumnId, filterValue) => {
				const val = row.getValue(filterColumnId) as string | undefined;
				if (!val) {
					return false;
				}
				if (val.includes(filterValue)) {
					return true;
				}
				return false;
			},
		},
	});

	const rawJsonColumn = table.getColumn("rawJson");

	const [searchColumn, setSearchColumn] = useState<Column<TData> | undefined>(
		rawJsonColumn,
	);

	const [searchValue, setSearchValue] = useState<string | undefined>(undefined);

	// Debounce setting the search column value so we don't refilter on every single character change
	useEffect(() => {
		const timeoutId = setTimeout(() => {
			searchColumn?.setFilterValue(searchValue);
		}, 500);
		return () => clearTimeout(timeoutId);
	}, [searchValue, searchColumn?.setFilterValue]);

	function updateSearchColumn(newSearchColumn?: Column<TData>) {
		searchColumn?.setFilterValue(undefined);
		setSearchValue(undefined);
		setSearchColumn(newSearchColumn ?? undefined);
	}

	return (
		<>
			<div className="flex gap-2">
				{searchColumn && (
					<Input
						value={searchValue}
						onChange={(e) => setSearchValue(e.target.value)}
						placeholder={`Search ${searchColumn.id}`}
					/>
				)}
				<Button type="button" onClick={() => updateSearchColumn(rawJsonColumn)}>
					Search raw JSON
				</Button>
				{searchColumn && (
					<Button type="button" onClick={() => updateSearchColumn()}>
						Cancel search
					</Button>
				)}
			</div>
			<div className="rounded-md border w-full">
				<Table>
					<TableHeader>
						{table.getHeaderGroups().map((headerGroup) => (
							<TableRow key={headerGroup.id}>
								{headerGroup.headers.map((header) => {
									return (
										<TableHead
											key={header.id}
											onClick={() => updateSearchColumn(header.column)}
										>
											{header.isPlaceholder
												? null
												: flexRender(
														header.column.columnDef.header,
														header.getContext(),
													)}
										</TableHead>
									);
								})}
							</TableRow>
						))}
					</TableHeader>
					<TableBody>
						{table.getRowModel().rows?.length ? (
							table.getRowModel().rows.map((row) => (
								<TableRow
									key={row.id}
									data-state={row.getIsSelected() && "selected"}
								>
									{row.getVisibleCells().map((cell) => (
										<TableCell key={cell.id}>
											{flexRender(
												cell.column.columnDef.cell,
												cell.getContext(),
											)}
										</TableCell>
									))}
								</TableRow>
							))
						) : (
							<TableRow>
								<TableCell
									colSpan={columns.length}
									className="h-24 text-center"
								>
									No results.
								</TableCell>
							</TableRow>
						)}
					</TableBody>
				</Table>
			</div>
		</>
	);
}
