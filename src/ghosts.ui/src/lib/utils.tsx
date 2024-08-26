import {
	Accordion,
	AccordionContent,
	AccordionItem,
	AccordionTrigger,
} from "@/components/ui/accordion";
import { useToast } from "@/components/ui/use-toast";
import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
	return twMerge(clsx(inputs));
}

/**
 * Returns a function to wrap async function calls on client.
 * The returned function awaits the async function and catches any errors.
 * Displays a toast for success and error cases
 */
export function useApiCall() {
	const { toast } = useToast();
	async function call<TData>(apiCall: Promise<TData>, successMsg: string) {
		try {
			const data = await apiCall;
			toast({ title: "Success", description: successMsg });
			return data;
		} catch (err) {
			toast({
				title: "Error",
				className: "flex flex-col gap-2 items-start h-min",
				action: (
					<Accordion type="multiple">
						<AccordionItem value="errorInfo">
							<AccordionTrigger>More info</AccordionTrigger>
							<AccordionContent>{JSON.stringify(err)}</AccordionContent>
						</AccordionItem>
					</Accordion>
				),
			});
		}
	}
	return call;
}
