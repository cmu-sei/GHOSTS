import { FormInput, FormSelect, FormSwitch } from "@/components/input";
import {
	Accordion,
	AccordionContent,
	AccordionItem,
	AccordionTrigger,
} from "@/components/ui/accordion";
import AutoFormObject from "@/components/ui/auto-form/fields/object";
import { Button } from "@/components/ui/button";
import {
	Card,
	CardContent,
	CardFooter,
	CardHeader,
	CardTitle,
} from "@/components/ui/card";
import { FormLabel } from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { SelectGroup, SelectItem } from "@/components/ui/select";
import { TabsContent } from "@/components/ui/tabs";
import { HANDLER_TYPES, type HandlerType } from "@/generated/endpoints";
import { HANDLER_ARGS_SCHEMAS, type NewTimeLine } from "@/lib/validation";
import { Plus, Trash } from "lucide-react";
import { useEffect, useState } from "react";
import {
	type FieldArrayWithId,
	type UseFormReturn,
	useFieldArray,
} from "react-hook-form";
import { type AnyZodObject, z } from "zod";
import { timeLineHandlerName } from "./timeline-form";
import { TimeLineFormEvents } from "./timeline-form-events";

/**
 * Renders a single handler in a timeline form
 */
export function TimeLineFormHandler({
	timeLineHandler,
	form,
	i,
	removeOuter,
}: {
	removeOuter: (i: number) => void;
	i: number;
	form: UseFormReturn<NewTimeLine>;
	timeLineHandler: FieldArrayWithId<NewTimeLine, "timeLineHandlers", "id">;
}) {
	const basePath = `timeLineHandlers.${i}` as const;

	const {
		fields: timeLineEvents,
		append,
		remove,
	} = useFieldArray({
		control: form.control,
		name: `${basePath}.timeLineEvents`,
	});
	return (
		<TabsContent value={timeLineHandler.id}>
			<Card>
				<CardHeader>
					<CardTitle>
						{timeLineHandlerName(form.watch(`timeLineHandlers.${i}`), i)}
					</CardTitle>
				</CardHeader>
				<CardContent className="flex flex-col gap-4">
					<FormSelect
						fieldName={`${basePath}.handlerType`}
						label="Handler type"
						form={form}
					>
						<SelectGroup>
							{HANDLER_TYPES.map((handlerType) => (
								<SelectItem key={handlerType} value={handlerType}>
									{handlerType}
								</SelectItem>
							))}
						</SelectGroup>
					</FormSelect>
					<FormInput
						label="Initial"
						form={form}
						fieldName={`${basePath}.initial`}
					/>

					<HandlerArgs form={form} basePath={basePath} />

					<div className="flex gap-2 items-center">
						<FormInput
							label="Utc time on"
							fieldName={`${basePath}.utcTimeOn`}
							form={form}
						/>
						<FormInput
							label="Utc time off"
							fieldName={`${basePath}.utcTimeOff`}
							form={form}
						/>
					</div>
					<FormSwitch form={form} label="Loop" fieldName={`${basePath}.loop`} />
					<div className="flex gap-2 items-center">
						<FormInput
							label="Schedule type"
							fieldName={`${basePath}.scheduleType`}
							form={form}
						/>
						<FormInput
							label="Schedule"
							fieldName={`${basePath}.schedule`}
							form={form}
						/>
					</div>
					<FormLabel>TimeLine events</FormLabel>
					<AddHandlerEventButton append={append} />
					{timeLineEvents.map((timeLineEvent, j) => (
						<TimeLineFormEvents
							key={timeLineEvent.id}
							{...{ timeLineEvent, j, i, form, remove }}
						/>
					))}
				</CardContent>
				<CardFooter>
					<Button
						type="button"
						onClick={(e) => {
							e.preventDefault();
							removeOuter(i);
						}}
					>
						<Trash className="w-4 h-4 m-0" />
					</Button>
				</CardFooter>
			</Card>
		</TabsContent>
	);
}

function AddHandlerEventButton({
	append,
}: {
	append: (
		event: NonNullable<
			NewTimeLine["timeLineHandlers"][number]["timeLineEvents"]
		>[number],
	) => void;
}) {
	return (
		<Button
			type="button"
			onClick={(e) => {
				e.preventDefault();
				append({
					command: "",
					commandArgs: [],
					delayAfter: 0,
					delayBefore: 0,
				});
			}}
		>
			<Plus className="w-4 h-4 m-0" />
		</Button>
	);
}

function getDefaultHandlerArgs(handlerType: HandlerType) {
	return (
		HANDLER_ARGS_SCHEMAS[handlerType as keyof typeof HANDLER_ARGS_SCHEMAS] ??
		z.object({})
	);
}

/**
 *  Renders the inputs for predefined handler args and allows the addition of custom handler args
 */
function HandlerArgs({
	form,
	basePath,
}: {
	form: UseFormReturn<NewTimeLine>;
	basePath: `timeLineHandlers.${number}`;
}) {
	const handlerType = form.watch(basePath).handlerType;

	// The name/key of a new arg we want to add
	const [newHandlerArgName, setNewHandlerArgName] = useState<
		string | undefined
	>(undefined);

	// When we add new keys to handler args we also want to update the schema
	// because it validates the handler args and also renders the inputs using auto-form
	const [handlerArgsSchema, setHandlerArgsSchema] = useState<AnyZodObject>(
		getDefaultHandlerArgs(handlerType),
	);

	// Update the schema whenever the handler changes
	// ie. when the handler type of a handler is changed or the visible handler is changed
	useEffect(() => {
		setHandlerArgsSchema(getDefaultHandlerArgs(handlerType));
	}, [handlerType]);

	return (
		<Accordion type="multiple">
			<AccordionItem value="predefinedHandlerArgs">
				<AccordionTrigger>Handler args</AccordionTrigger>
				<AccordionContent className="flex flex-col gap-4">
					<Input
						value={newHandlerArgName}
						onChange={(e) => setNewHandlerArgName(e.target.value)}
						placeholder="Handler arg name"
					/>
					<Button
						onClick={(e) => {
							e.preventDefault();
							if (newHandlerArgName) {
								setHandlerArgsSchema((handlerArgsSchema) =>
									handlerArgsSchema.extend({
										// TODO: allow custom handler args to have other datatype than string
										[newHandlerArgName]: z.string(),
									}),
								);
								setNewHandlerArgName(undefined);
							}
						}}
					>
						Add handler arg
					</Button>
					<Button
						onClick={(e) => {
							e.preventDefault();
							setHandlerArgsSchema(getDefaultHandlerArgs(handlerType));
						}}
					>
						Remove custom handler args
					</Button>
					<AutoFormObject
						schema={handlerArgsSchema}
						form={form as any}
						path={[`${basePath}.handlerArgs`]}
					/>
				</AccordionContent>
			</AccordionItem>
		</Accordion>
	);
}
