import { FormInput } from "@/components/input";
import {
	Accordion,
	AccordionContent,
	AccordionItem,
	AccordionTrigger,
} from "@/components/ui/accordion";
import { Button } from "@/components/ui/button";
import type { NewTimeLine } from "@/lib/validation";
import { Trash } from "lucide-react";
import type { UseFormReturn } from "react-hook-form";

/**
 * Renders a single event of a timeline handler in a timeline form
 */
export function TimeLineFormEvents({
	form,
	remove,
	j,
	i,
}: {
	i: number;
	j: number;
	remove: (j: number) => void;
	form: UseFormReturn<NewTimeLine>;
}) {
	const basePath = `timeLineHandlers.${i}.timeLineEvents.${j}` as const;
	const basePathWatch = form.watch(basePath);
	return (
		<Accordion type="multiple" className="w-full">
			<AccordionItem value="test">
				<AccordionTrigger>{`${j + 1} : ${
					basePathWatch.command ? basePathWatch.command : "No command yet"
				}`}</AccordionTrigger>
				<AccordionContent>
					<FormInput
						form={form}
						fieldName={`${basePath}.command`}
						label="Command"
					/>

					<FormInput
						form={form}
						fieldName={`${basePath}.commandArgs`}
						label="Command args (comma separated)"
						parseCommaSeparatedStringAsArray
					/>

					<FormInput
						form={form}
						fieldName={`${basePath}.delayBefore`}
						parseInt
						label="Delay before"
					/>
					<FormInput
						form={form}
						fieldName={`${basePath}.delayAfter`}
						parseInt
						label="Delay after"
					/>
					<Button onClick={() => remove(j)}>
						<Trash className="w-4 h-4" />
					</Button>
				</AccordionContent>
			</AccordionItem>
		</Accordion>
	);
}
