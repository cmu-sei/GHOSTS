"use client";

import { FormInput } from "@/components/input";
import { Button } from "@/components/ui/button";
import { Form } from "@/components/ui/form";
import { api } from "@/generated/endpoints";
import { useApiCall } from "@/lib/utils";
import { type NewMachine, newMachineSchema } from "@/lib/validation";
import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";

export function MachineForm() {
	const router = useRouter();
	const form = useForm<NewMachine>({
		resolver: zodResolver(newMachineSchema),
	});
	const call = useApiCall();
	return (
		<Form {...form}>
			<form
				onSubmit={form.handleSubmit(async (data) => {
					await call(api.postApimachines(data), "Created new machine");
					router.push("/machines");
					router.refresh();
				})}
			>
				<FormInput form={form} label="Name" fieldName="name" />
				<Button type="submit">Create</Button>
			</form>
		</Form>
	);
}
