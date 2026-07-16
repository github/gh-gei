package bbs

// Project represents a Bitbucket Server project.
type Project struct {
	ID   int    `json:"id"`
	Key  string `json:"key"`
	Name string `json:"name"`
}

// Repository represents a Bitbucket Server repository.
type Repository struct {
	ID   int    `json:"id"`
	Slug string `json:"slug"`
	Name string `json:"name"`
}

// PullRequest represents a Bitbucket Server pull request.
type PullRequest struct {
	ID   int    `json:"id"`
	Name string `json:"name"`
}

// exportState represents the state of an export operation.
type exportState struct {
	State    string `json:"state"`
	Progress struct {
		Message    string `json:"message"`
		Percentage int    `json:"percentage"`
	} `json:"progress"`
}

// paginatedResponse is a generic BBS paginated API response.
type paginatedResponse[T any] struct {
	Values        []T  `json:"values"`
	Size          int  `json:"size"`
	IsLastPage    bool `json:"isLastPage"`
	Start         int  `json:"start"`
	Limit         int  `json:"limit"`
	NextPageStart int  `json:"nextPageStart,omitempty"`
}
