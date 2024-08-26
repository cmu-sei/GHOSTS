import { H1 } from "@/components/text";
import { Button } from "@/components/ui/button";
import { db, timeLinesTable } from "@/lib/db";
import Link from "next/link";
import type React from "react";
import { TimeLineTable } from "./timeline-table";

export default async function Layout({
	children,
}: { children: React.ReactNode }) {
	const timeLines = await db.select().from(timeLinesTable);
	return (
		<>
			<H1>TimeLines</H1>
			<div className="flex gap-2">
				<Button asChild>
					<Link href="/timelines/new">New timeLine</Link>
				</Button>
			</div>
			<TimeLineTable timeLines={timeLines} />
			{children}
		</>
	);
}
