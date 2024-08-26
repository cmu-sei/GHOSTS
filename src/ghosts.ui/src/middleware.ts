import { type NextRequest, NextResponse } from "next/server";
import { CLIENT_GHOSTS_API_PATH, GHOSTS_API_URL } from "./generated/endpoints";

export default function middleware(req: NextRequest) {
	const pathName = req.nextUrl.pathname;

	// Requests from our client to the GHOSTS API start with this path
	// If this is the case, we rewrite it to the GHOSTS API
	// This way we can control the GHOSTS API Url from env var instead of hardcoding and sending it to client
	if (pathName.startsWith(CLIENT_GHOSTS_API_PATH)) {
		const newUrl = new URL(GHOSTS_API_URL);
		newUrl.pathname = pathName.slice(CLIENT_GHOSTS_API_PATH.length);
		return NextResponse.rewrite(newUrl);
	}
	return;
}
