/**
 * Ensure that all headings are the same size
 */
export function H1({ children }: { children: string }) {
	return <h1 className="text-2xl">{children}</h1>;
}
