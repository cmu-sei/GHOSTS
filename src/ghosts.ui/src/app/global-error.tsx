"use client";

import {
	Accordion,
	AccordionContent,
	AccordionItem,
	AccordionTrigger,
} from "@/components/ui/accordion";
import { Button } from "@/components/ui/button";

/**
 * Next.js default error component
 */
export default function ErrorPage({
	error,
	reset,
}: {
	error: Error & { digest?: string };
	reset: () => void;
}) {
	return (
		<>
			<h2>Something went wrong!</h2>
			<Accordion className="w-[360px]" type="multiple">
				<AccordionItem value="Error">
					<AccordionTrigger>Details</AccordionTrigger>
					<AccordionContent>
						<p>{error.message}</p>
					</AccordionContent>
				</AccordionItem>
			</Accordion>
			<Button type="reset" onClick={() => reset()}>
				Try again
			</Button>
		</>
	);
}
