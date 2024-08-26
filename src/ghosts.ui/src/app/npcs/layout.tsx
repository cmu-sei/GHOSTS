import { H1 } from "@/components/text";
import { Button } from "@/components/ui/button";
import { api } from "@/generated/endpoints";
import Link from "next/link";
import type React from "react";
import { NpcTable } from "./npc-table";

export default async function Layout({
	children,
}: { children: React.ReactNode }) {
	const npcs = await api.getApinpcs();
	const machines = await api.getApimachines();

	return (
		<>
			<H1>Npcs</H1>
			<div className="flex gap-2">
				<Button asChild>
					<Link href="/npcs/manual">{"Manually create an NPC"}</Link>
				</Button>
				<Button asChild>
					<Link href="/npcs/generate">{"Generate random NPC's"}</Link>
				</Button>
			</div>
			<NpcTable npcs={npcs} machines={machines} />
			{children}
		</>
	);
}
