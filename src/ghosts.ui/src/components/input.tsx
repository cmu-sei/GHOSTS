import type { PropsWithChildren } from "react";
import type {
	ControllerRenderProps,
	FieldValues,
	Path,
	UseFormReturn,
} from "react-hook-form";
import {
	FormControl,
	FormDescription,
	FormField,
	FormItem,
	FormLabel,
	FormMessage,
} from "./ui/form";
import { Input } from "./ui/input";
import { Select, SelectContent, SelectTrigger, SelectValue } from "./ui/select";
import { Switch } from "./ui/switch";

/**
 * Utility wrappers around inputs
 */

type FormComponentProps<TFieldValues extends FieldValues> = {
	form: UseFormReturn<TFieldValues>;
	fieldName: Path<TFieldValues>;
	label?: string;
};

function InputWrapper<TFieldValues extends FieldValues>({
	form,
	fieldName,
	label,
	renderInput,
}: FormComponentProps<TFieldValues> & {
	renderInput: (
		field: ControllerRenderProps<TFieldValues, Path<TFieldValues>>,
	) => React.ReactNode;
}) {
	return (
		<FormField
			control={form.control}
			name={fieldName}
			render={({ field }) => (
				<FormItem className="flex flex-col">
					{label && <FormLabel>{label}</FormLabel>}
					<FormControl>{renderInput(field)}</FormControl>
					<FormDescription />
					<FormMessage />
				</FormItem>
			)}
		/>
	);
}

export function FormSwitch<TFieldValues extends FieldValues>(
	props: FormComponentProps<TFieldValues>,
) {
	return (
		<InputWrapper
			{...props}
			renderInput={({ value, onChange, ...props }) => (
				<Switch checked={value} onCheckedChange={onChange} {...props} />
			)}
		/>
	);
}

export function FormInput<TFieldValues extends FieldValues>(
	props: FormComponentProps<TFieldValues> & {
		parseCommaSeparatedStringAsArray?: boolean;
		parseInt?: boolean;
	},
) {
	return (
		<InputWrapper
			{...props}
			renderInput={(field) => (
				<Input
					{...field}
					onChange={(e) => {
						if (props.parseCommaSeparatedStringAsArray) {
							field.onChange(e.target.value.split(","));
						} else if (props.parseInt) {
							const int = Number.parseInt(e.target.value);
							if (!Number.isNaN(int)) {
								field.onChange(int);
							} else {
								field.onChange(e);
							}
						} else {
							field.onChange(e);
						}
					}}
				/>
			)}
		/>
	);
}

export function FormSelect<TFieldValues extends FieldValues>({
	children,
	parseAsInt,
	...props
}: FormComponentProps<TFieldValues> & {
	parseAsInt?: boolean;
} & PropsWithChildren) {
	return (
		<InputWrapper
			{...props}
			renderInput={(field) => (
				<Select
					onValueChange={(val) => field.onChange(parseAsInt ? +val : val)}
					value={field.value}
				>
					<SelectTrigger>
						<SelectValue placeholder={props.label} />
					</SelectTrigger>
					<SelectContent>{children}</SelectContent>
				</Select>
			)}
		/>
	);
}
