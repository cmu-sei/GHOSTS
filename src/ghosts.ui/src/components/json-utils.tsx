import { CopyCheck } from "lucide-react";
import { useEffect, useState } from "react";
import type { ZodType, z } from "zod";
import { Button } from "./ui/button";
import { Popover, PopoverContent, PopoverTrigger } from "./ui/popover";
import { Textarea } from "./ui/textarea";
import { useToast } from "./ui/use-toast";

/**
 * A popover to view and copy the provided json
 */
export function JSONViewer<T>({ json }: { json: T }) {
	const jsonString = JSON.stringify(json, undefined, 2);
	return (
		<Popover>
			<PopoverTrigger className="ml-2">View JSON</PopoverTrigger>
			<PopoverContent className="max-h-96 w-min p-8 overflow-scroll">
				<CopyCheck
					className="w-4 h-4 absolute top-2 right-2 cursor-pointer"
					onClick={async () => await navigator.clipboard.writeText(jsonString)}
				/>
				<pre>{jsonString}</pre>
			</PopoverContent>
		</Popover>
	);
}

/**
 * Renders a textarea, validates the provided input against the validator and calls onValidData if the data is valid
 * If there is existing data to edit, use the prefillData prop
 */
export function JSONImporter<
	TValidator extends ZodType,
	TData extends z.infer<TValidator>,
>({
	validator,
	onValidData,
	prefillData,
}: {
	validator: TValidator;
	onValidData: (data: TData) => void;
	prefillData?: TData;
}) {
	const prefillDataString = prefillData
		? JSON.stringify(prefillData, undefined, 4)
		: "";
	const [jsonToImport, setJsonToImport] = useState(prefillDataString);

	// Ensure prefilldata is up to date
	useEffect(() => setJsonToImport(prefillDataString), [prefillDataString]);

	const { toast } = useToast();
	return (
		<Popover>
			<PopoverTrigger asChild>
				<Button>{prefillData ? "Edit as JSON" : "Import from JSON"}</Button>
			</PopoverTrigger>
			<PopoverContent>
				<Textarea
					className="w-[400px] h-[600px] overflow-scroll"
					value={jsonToImport}
					onChange={(e) => {
						try {
							// Pretty print JSON if valid
							setJsonToImport(
								JSON.stringify(JSON.parse(e.target.value), undefined, 4),
							);
						} catch {
							setJsonToImport(e.target.value);
						}
					}}
					onBlur={(event) => {
						let json: unknown;
						try {
							json = JSON.parse(event.target.value);
						} catch (e) {
							toast({
								title: "Invalid JSON",
							});
							return;
						}

						const result = validator.safeParse(json);
						if (!result.success) {
							toast({
								title: "JSON is not a valid array of timeline handlers",
								description: result.error.message,
							});
						} else {
							toast({
								title: "JSON import success",
							});
							onValidData(result.data);
						}
					}}
				/>
			</PopoverContent>
		</Popover>
	);
}
