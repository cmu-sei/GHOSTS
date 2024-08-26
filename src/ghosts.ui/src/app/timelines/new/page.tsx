import { SheetWrapper } from "@/components/sheet-wrapper";
import { TimeLineForm } from "../timeline-form";

export default function Page() {
	return (
		<SheetWrapper returnPath="/timelines">
			<TimeLineForm />
		</SheetWrapper>
	);
}
