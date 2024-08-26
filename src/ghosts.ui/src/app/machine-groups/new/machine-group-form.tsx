"use client";

import { FormInput } from "@/components/input";
import { Button } from "@/components/ui/button";
import { Form } from "@/components/ui/form";
import { api } from "@/generated/endpoints";
import { useApiCall } from "@/lib/utils";
import { type NewGroup, newGroupSchema } from "@/lib/validation";
import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";

export function MachineGroupForm() {
	const router = useRouter();
	const form = useForm<NewGroup>({
		resolver: zodResolver(newGroupSchema),
	});
	const call = useApiCall();
	return (
		<Form {...form}>
			<form
				onSubmit={form.handleSubmit(async (data) => {
					await call(
						api.postApimachinegroups({ id: 0, ...data }),
						"Created new machine group",
					);
					router.push("/machine-groups");
					router.refresh();
				})}
			>
				<FormInput form={form} label="Name" fieldName="name" />
				<Button type="submit">Create</Button>
			</form>
		</Form>
	);
}
