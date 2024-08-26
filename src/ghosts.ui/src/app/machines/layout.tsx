import { H1 } from "@/components/text";
import { Button } from "@/components/ui/button";
import { api } from "@/generated/endpoints";
import { db, timeLinesTable } from "@/lib/db";
import Link from "next/link";
import type React from "react";
import { MachineTable } from "./machine-table";

export default async function Layout({
	children,
}: { children: React.ReactNode }) {
	const machines = await api.getApimachines();
	const timeLines = await db.select().from(timeLinesTable);
	return (
		<>
			<H1>Machines</H1>
			<div className="flex gap-2">
				<Button asChild>
					<Link href="/machines/new">New machine</Link>
				</Button>
			</div>
			<MachineTable machines={machines} />
			{children}
		</>
	);
}
