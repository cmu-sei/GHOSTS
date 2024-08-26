"use client";

import { SheetTitle } from "@/components/ui/sheet";
import {
	type GroupType,
	type MachineType,
	api,
	schemas,
	updateTypes,
} from "@/generated/endpoints";
import { useApiCall } from "@/lib/utils";
import type { TimeLine } from "@/lib/validation";
import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { type MutableRefObject, useRef } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormSelect } from "./input";
import { H1 } from "./text";
import { Button } from "./ui/button";
import { Form } from "./ui/form";
import { Popover, PopoverContent, PopoverTrigger } from "./ui/popover";
import { SelectItem } from "./ui/select";
import { SheetClose, SheetFooter } from "./ui/sheet";
import { useToast } from "./ui/use-toast";

export type MachineOrMachineGroup =
	| { machine: MachineType; machineGroup?: never }
	| { machine?: never; machineGroup: GroupType };

/**
 * Renders all the timeLines and allows the timeLines to be ran against a machine or machineGroup.
 */
export function TimeLineRunner({
	machine,
	machineGroup,
	timeLines,
	returnPath,
}: { timeLines: TimeLine[]; returnPath: string } & MachineOrMachineGroup) {
	const name = machine ? machine.name : machineGroup.name;
	const entityName = `${machine ? "machine" : "machine group"} '${name}'`;

	const timeLineRunnerSchema = z.object({
		updateType: schemas.UpdateType,
		timeLineId: z.number(),
	});
	type TimeLineRunner = z.infer<typeof timeLineRunnerSchema>;

	const form = useForm<TimeLineRunner>({
		resolver: zodResolver(timeLineRunnerSchema),
		defaultValues: {
			updateType: "TimelinePartial",
		},
	});

	const formRef = useRef<HTMLFormElement | null>(null);

	const call = useApiCall();
	const router = useRouter();
	const { toast } = useToast();

	async function runTimeLineForMachineOrMachineGroup(data: TimeLineRunner) {
		// This should always be found as we have selected a valid timeline from the dropdown
		// so we can set an empty array as fallback
		const timeLineHandlers =
			timeLines.find((timeLine) => timeLine.id === data.timeLineId)
				?.timeLineHandlers ?? [];

		if (machine) {
			await call(
				api.postTimeLines({
					machineId: machine.id,
					type: data.updateType,
					update: { status: "Run", timeLineHandlers },
				}),
				`Ran timeLine for ${entityName}`,
			);
		} else {
			// Loop through machines and post separately for each one
			// GHOSTS API endpoint /api/machineupdates/group/{groupId} doesn't work as expected so this is a temp workaround
			for (const { machineId } of machineGroup.groupMachines ?? []) {
				await call(
					api.postTimeLines({
						machineId,
						type: data.updateType,
						update: { timeLineHandlers },
					}),
					`Ran timeLine for machine ${machineId}`,
				);
			}
			toast({
				title: "Success",
				description: `Ran all timeLines for ${entityName}`,
			});
		}

		router.push(returnPath);
	}

	return (
		<>
			<SheetTitle>{`Run timeLine for ${entityName}`}</SheetTitle>
			<Form {...form}>
				<form
					ref={formRef}
					onSubmit={form.handleSubmit(runTimeLineForMachineOrMachineGroup)}
				>
					<FormSelect form={form} label="Update type" fieldName="updateType">
						{[updateTypes[0], updateTypes[2]].map((updateType) => (
							<SelectItem key={updateType} value={updateType}>
								{updateType}
							</SelectItem>
						))}
					</FormSelect>
					<FormSelect
						form={form}
						label="TimeLine"
						fieldName="timeLineId"
						parseAsInt
					>
						{timeLines.map((timeLine) => (
							<SelectItem key={timeLine.id} value={timeLine.id as any}>
								{timeLine.name}
							</SelectItem>
						))}
					</FormSelect>
					<ConfirmRunTimeline
						timeLineId={form.watch("timeLineId")}
						timeLines={timeLines}
						entityName={entityName}
						formRef={formRef}
					/>
				</form>
			</Form>
			<SheetFooter>
				<SheetClose>Cancel</SheetClose>
			</SheetFooter>
		</>
	);
}

function ConfirmRunTimeline({
	timeLines,
	timeLineId,
	entityName,
	formRef,
}: {
	timeLines: TimeLine[];
	timeLineId: number;
	entityName: string;
	formRef: MutableRefObject<HTMLFormElement | null>;
}) {
	const timelineToRun = timeLines.find((timeline) => {
		return timeline.id === timeLineId;
	});
	if (!timelineToRun) {
		return "Select a valid timeline to before running";
	}
	return (
		<Popover>
			<PopoverTrigger asChild>
				<Button>Run timeline</Button>
			</PopoverTrigger>
			<PopoverContent className="flex flex-col gap-4 p-8">
				<H1>Warning</H1>
				<p>
					{"You are about to run timeline "}
					<Link
						className="underline"
						href={`/timelines/edit/${timelineToRun.id}`}
					>
						{timelineToRun.name}
					</Link>
					{` for ${entityName}`}
				</p>
				<Button onClick={() => formRef.current?.requestSubmit()}>
					Run timeLine
				</Button>
			</PopoverContent>
		</Popover>
	);
}
