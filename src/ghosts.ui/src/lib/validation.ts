import { type HandlerType, schemas } from "@/generated/endpoints";
import { type AnyZodObject, z } from "zod";

// Utility types and validators for creating new machines and groups

export type NewMachine = z.infer<typeof newMachineSchema>;

export const newMachineSchema = z.object({
	name: z.string(),
});

export type NewGroup = z.infer<typeof newGroupSchema>;

export const newGroupSchema = z.object({
	name: z.string().min(1),
});

const probability = z.number().min(0).max(100);

/**
 * Prefilled handler args validators for BrowserFirefox and Notepad handler types
 * Handler args validators for other handler types can be added here
 */
export const HANDLER_ARGS_SCHEMAS = {
	/**
	 * @see https://cmu-sei.github.io/GHOSTS/core/handlers/browser/#timeLine-settings
	 */
	BrowserFirefox: z
		.object({
			isheadless: z.boolean(),
			blockimages: z.boolean(),
			blockstyles: z.boolean(),
			blockflash: z.boolean(),
			blockscripts: z.boolean(),
			stickiness: probability,
			"stickiness-depth-min": z.number(),
			"stickiness-depth-max": z.number(),
			incognito: z.boolean(),
			"url-replace": z
				.string()
				.describe("Input url-replaced as JSON")
				.superRefine((string, ctx) => {
					const result = z
						.array(z.record(z.array(z.string())))
						.safeParse(string);
					if (!result.success) {
						ctx.addIssue({
							code: z.ZodIssueCode.custom,
							message: "JSON is not a valid 'url-replace configuration'",
						});
						return z.NEVER;
					}
					return result.data;
				}),
		})
		.partial(),
	/**
	 * @see https://cmu-sei.github.io/GHOSTS/core/handlers/notepad/
	 */
	Notepad: z
		.object({
			"execution-probability": probability,
			"deletion-probability": probability,
			"view-probability": probability,
			"creation-probability": probability,
			"modification-probability": probability,
			"pdf-probability": probability,
			"input-directory": z.string(),
			"output-directory": z.string(),
			"text-generation": z.string(),
			"min-paragraphs": z.number(),
			"max-paragraphs": z.number(),
			"delay-jitter": z.number(),
		})
		.partial(),
} as const satisfies { [key in HandlerType]?: AnyZodObject | undefined };

// Types and validators used for timelines stored in the SQLite db

export type NewTimeLine = z.infer<typeof newTimeLineSchema>;

export const newTimeLineSchema = z.object({
	name: z.string(),
	timeLineHandlers: z.array(schemas.TimeLineHandler),
});

export type TimeLine = z.infer<typeof timeLineSchema>;

export const timeLineSchema = newTimeLineSchema.extend({ id: z.number() });
