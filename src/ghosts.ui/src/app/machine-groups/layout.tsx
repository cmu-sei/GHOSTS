import { H1 } from "@/components/text";
import { MachineGroupTable } from "./machine-group-table";

import { Button } from "@/components/ui/button";
import { api } from "@/generated/endpoints";
import Link from "next/link";
import type React from "react";

export default async function Page({
	children,
}: { children: React.ReactNode }) {
	const machines = await api.getApimachines();
	const machineGroups = await api.getApimachinegroups();
	return (
		<>
			<H1>Machine groups</H1>
			<div className="flex gap-2">
				<Button asChild>
					<Link href="/machine-groups/new">New machine group</Link>
				</Button>
			</div>
			<MachineGroupTable allMachines={machines} machineGroups={machineGroups} />
			{children}
		</>
	);
}
