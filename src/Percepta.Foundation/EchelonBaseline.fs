namespace Percepta.Foundation

open Aegis
open Aegis.Integration.GitHub
open Aegis.Store.GitHub

/// Compile-time proof that Percepta references the complete Aegis baseline.
/// Application-specific policies and storage configuration belong in later bounded work.
module EchelonBaseline =
    let faultSchema = Schema.Fault

    let githubFailureType : GitHubFailure.T option = None

    let githubCommitStrategy : GitHubStore.CommitStrategy option = None
