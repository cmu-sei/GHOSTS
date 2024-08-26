"use client";

import { FormInput } from "@/components/input";
import { Button } from "@/components/ui/button";
import { Form } from "@/components/ui/form";
import { Separator } from "@/components/ui/separator";
import { api } from "@/generated/endpoints";
import { useApiCall } from "@/lib/utils";
import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";

export function NpcGenerator() {
	const router = useRouter();

	const call = useApiCall();

	return (
		<>
			<Button
				onClick={async () => {
					await call(api.postApinpcsgenerateone(undefined), "Generated NPC");
					router.push("/npcs");
					router.refresh();
				}}
			>
				Generate one
			</Button>
			<Separator className="my-4" />
			<GenerateNpcsForm />
		</>
	);
}

function GenerateNpcsForm() {
	const generateNpcsSchema = z.object({
		campaign: z.string(),
		enclave: z.string(),
		team: z.string(),
		count: z.number().int(),
	});

	type GenerateNpcs = z.infer<typeof generateNpcsSchema>;

	const generateNpcsForm = useForm<GenerateNpcs>({
		resolver: zodResolver(generateNpcsSchema),
	});

	const router = useRouter();
	const call = useApiCall();

	async function generateNpcsForCampaignEnclaveTeam(data: GenerateNpcs) {
		await call(
			api.postApinpcsgenerate({
				campaign: data.campaign,
				enclaves: [
					{
						name: data.enclave,
						teams: [
							{
								name: data.team,
								npcs: {
									number: data.count,
								},
							},
						],
					},
				],
			}),
			`Generated ${data.count} NPC's`,
		);
		router.push("/npcs");
		router.refresh();
	}

	return (
		<Form {...generateNpcsForm}>
			<form
				onSubmit={generateNpcsForm.handleSubmit(
					generateNpcsForCampaignEnclaveTeam,
				)}
			>
				<FormInput
					label="Campaign"
					form={generateNpcsForm}
					fieldName="campaign"
				/>
				<FormInput
					label="Enclave"
					form={generateNpcsForm}
					fieldName="enclave"
				/>
				<FormInput label="Team" form={generateNpcsForm} fieldName="team" />
				<FormInput
					label="Count"
					form={generateNpcsForm}
					fieldName="count"
					parseInt
				/>

				<Button type="submit">Generate for campaign, enclave, team</Button>
			</form>
		</Form>
	);
}
