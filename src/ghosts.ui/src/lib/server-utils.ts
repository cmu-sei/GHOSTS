import type { MachineActivityType } from "@/generated/endpoints";
import { redirect } from "next/navigation";
import { z } from "zod";

export function getPaginationSearchParams(
	searchParamsObj: unknown,
	redirectUrl: string,
) {
	const result = z
		.object({ skip: z.coerce.number().min(0), take: z.coerce.number().min(1) })
		.safeParse(searchParamsObj);
	if (!result.success) redirect(redirectUrl);

	return result.data;
}
/**
 * Parses the pagination search params, fetches activity based on apiCall arg and sorts them newest to oldest.
 *
 * @param apiCall either api.getApimachinesIdactivity or api.getApimachinegroupsIdactivity. Both of these return the oldest record first (WTF??).
 * So when we fetch records, we get X of the oldest ones, only these X oldest are sorted newest first. This is not optimal but theres nothing to do.
 */
export async function getSortedActivity<TIdType extends string | number>(
	apiCall: (input: {
		params: { id: TIdType };
		queries: { take: number; skip: number };
	}) => Promise<MachineActivityType[]>,
	id: TIdType,
	searchParamsObj: unknown,
	redirectUrl: string,
) {
	const paginationSearchParams = getPaginationSearchParams(
		searchParamsObj,
		redirectUrl,
	);
	const activity = await apiCall({
		params: { id },
		queries: paginationSearchParams,
	});
	return activity.toSorted(
		(a, b) =>
			new Date(b.createdUtc).getTime() - new Date(a.createdUtc).getTime(),
	);
}
