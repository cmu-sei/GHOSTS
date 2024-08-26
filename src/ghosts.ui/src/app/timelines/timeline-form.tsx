"use client";

import { FormInput } from "@/components/input";
import { JSONImporter } from "@/components/json-utils";
import { Button } from "@/components/ui/button";
import {
	DropdownMenu,
	DropdownMenuContent,
	DropdownMenuItem,
	DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Form, FormLabel } from "@/components/ui/form";
import { Tabs } from "@/components/ui/tabs";
import { useApiCall } from "@/lib/utils";
import {
	type NewTimeLine,
	type TimeLine,
	newTimeLineSchema,
} from "@/lib/validation";
import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import {
	type FieldArrayWithId,
	type UseFormReturn,
	useFieldArray,
	useForm,
} from "react-hook-form";
import { createTimeLine, updateTimeLine } from "./action";
import { TimeLineFormHandler } from "./timeline-form-handlers";

export function timeLineHandlerName(
	timeLineHandler: TimeLine["timeLineHandlers"][number],
	i: number,
) {
	return `${i + 1} : ${timeLineHandler.handlerType ?? "No handler type yet"}`;
}

export function TimeLineForm({ timeLine }: { timeLine?: TimeLine }) {
	const form = useForm<NewTimeLine>({
		resolver: zodResolver(newTimeLineSchema),
		defaultValues: timeLine,
	});

	const {
		fields: timeLineHandlers,
		append,
		remove,
	} = useFieldArray({
		control: form.control,
		name: "timeLineHandlers",
	});

	// Keep track of which handler is currently being edited
	const [openHandler, setOpenHandler] = useState<string | undefined>();

	// Ensure that a handler is always visible if one exists
	useEffect(() => {
		setOpenHandler(timeLineHandlers[0]?.id);
	}, [timeLineHandlers]);

	const call = useApiCall();
	const router = useRouter();

	async function createOrUpdateTimeLine(data: NewTimeLine) {
		if (timeLine) {
			await call(
				updateTimeLine({ id: timeLine.id, ...data }),
				"Updated timeLine",
			);
		} else {
			await call(createTimeLine(data), "Created timeLine");
		}
		router.push("/timelines");
	}

	return (
		<Form {...form}>
			<form
				className="flex flex-col items-start gap-4 w-full"
				onSubmit={form.handleSubmit(createOrUpdateTimeLine)}
			>
				<FormInput fieldName="name" label="Name" form={form} />
				<FormLabel>TimeLine handlers</FormLabel>
				<div className="flex gap-2">
					<JSONImporter
						prefillData={timeLine?.timeLineHandlers}
						validator={newTimeLineSchema.shape.timeLineHandlers}
						onValidData={(data) => {
							form.setValue("timeLineHandlers", data);
						}}
					/>
					<AddHandlerButton append={append} />
				</div>
				<HandlerToEditSelector
					form={form}
					timeLineHandlers={timeLineHandlers}
					setOpenHandler={setOpenHandler}
				/>

				<Tabs
					className="w-full"
					value={openHandler}
					onValueChange={setOpenHandler}
				>
					{timeLineHandlers.map((timeLineHandler, i) => (
						<TimeLineFormHandler
							key={timeLineHandler.id}
							{...{ timeLineHandler, i, form, removeOuter: remove }}
						/>
					))}
				</Tabs>
				<Button type="submit">{timeLine ? "Update" : "Create"}</Button>
			</form>
		</Form>
	);
}

/**
 * A button that adds a new handler to the timeline form
 */
function AddHandlerButton({
	append,
}: { append: (handler: TimeLine["timeLineHandlers"][number]) => void }) {
	return (
		<Button
			type="button"
			onClick={(e) => {
				e.preventDefault();
				append({
					handlerType: "Command",
					initial: "",
					utcTimeOn: "00:00:00",
					utcTimeOff: "24:00:00",
					handlerArgs: {},
					timeLineEvents: [],
					loop: true,
				});
			}}
		>
			Add handler
		</Button>
	);
}

/**
 * Renders a dropdown that allows the user to pick a handler to edit
 */
function HandlerToEditSelector({
	form,
	timeLineHandlers,
	setOpenHandler,
}: {
	form: UseFormReturn<NewTimeLine>;
	timeLineHandlers: FieldArrayWithId<TimeLine, "timeLineHandlers">[];
	setOpenHandler: (id: string) => void;
}) {
	return (
		<DropdownMenu>
			<DropdownMenuTrigger asChild>
				<Button variant="outline">{`Select handler to edit (${timeLineHandlers.length})`}</Button>
			</DropdownMenuTrigger>
			<DropdownMenuContent>
				{timeLineHandlers.map((timeLineHandler, i) => (
					<DropdownMenuItem
						key={timeLineHandler.id}
						onClick={() => setOpenHandler(timeLineHandler.id)}
					>
						{timeLineHandlerName(form.watch(`timeLineHandlers.${i}`), i)}
					</DropdownMenuItem>
				))}
			</DropdownMenuContent>
		</DropdownMenu>
	);
}
